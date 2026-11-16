"use client";

import { makeStyles, tokens } from "@fluentui/react-components";
import { useCallback, useEffect, useState } from "react";
import { ChatView } from "@/components/chat/ChatView";
import { ConversationsPanel, SavedChatsNotice, type SavedChatsActions } from "@/components/savedChats/ConversationsPanel";
import { chatErrorMessage, type LocalizedError } from "@/lib/chat/chatErrors";
import { sortFolders, titleFrom, UNTITLED_TITLE, upsertConversation } from "@/lib/chat/savedChats";
import { useI18n } from "@/lib/i18n/I18nProvider";
import * as savedChats from "@/services/conversationsService";
import type { ConversationSummary, Folder } from "@/services/types";

const useStyles = makeStyles({
  workspace: {
    display: "flex",
    flexGrow: 1,
    minWidth: 0,
    "@media (max-width: 899px)": { flexDirection: "column" },
  },
  panel: {
    flexShrink: 0,
    width: "280px",
    boxSizing: "border-box",
    padding: `${tokens.spacingVerticalXXL} ${tokens.spacingHorizontalM}`,
    borderInlineEndWidth: tokens.strokeWidthThin,
    borderInlineEndStyle: "solid",
    borderInlineEndColor: tokens.colorNeutralStroke2,
    "@media (max-width: 899px)": {
      width: "100%",
      maxHeight: "40vh",
      overflowY: "auto",
      borderInlineEndStyle: "none",
      borderBottom: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralStroke2}`,
    },
  },
  chat: { flexGrow: 1, minWidth: 0, display: "flex", flexDirection: "column" },
  notice: { padding: `${tokens.spacingVerticalM} ${tokens.spacingHorizontalXXL} 0` },
});

const hrefOf = (id: string) => `/chat?c=${encodeURIComponent(id)}`;

/** The open conversation in the address bar, so a reload or a shared link reopens it (no Next.js navigation). */
function showInAddressBar(id: string | null) {
  window.history.replaceState(window.history.state, "", id ? hrefOf(id) : "/chat");
}

const asError = (failure: unknown): LocalizedError => chatErrorMessage(failure);

/**
 * The chat page with its saved chats (docs/specs/feature-saved-chats.md): the panel of folders and conversations next to
 * the chat. Opening a conversation or starting a new one remounts the chat (`session`); a chat that saves itself for the
 * first time only updates the list and the address bar.
 */
export function ChatWorkspace({ initialConversationId = null }: { initialConversationId?: string | null }) {
  const styles = useStyles();
  const { t } = useI18n();
  const [listState, setListState] = useState<"loading" | "ready" | "failed">("loading");
  const [folders, setFolders] = useState<Folder[]>([]);
  const [conversations, setConversations] = useState<ConversationSummary[]>([]);
  const [activeId, setActiveId] = useState<string | null>(initialConversationId);
  const [session, setSession] = useState({ key: 0, conversationId: initialConversationId });
  const [notice, setNotice] = useState<string | null>(null);

  // The list starts as "loading"; a retry sets it again before calling this.
  const load = useCallback((signal?: AbortSignal) => {
    Promise.all([savedChats.listFolders(signal), savedChats.listConversations(signal)])
      .then(([loadedFolders, loadedConversations]) => {
        setFolders(sortFolders(loadedFolders));
        setConversations(loadedConversations);
        setListState("ready");
      })
      .catch(() => {
        if (!signal?.aborted) setListState("failed");
      });
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    load(controller.signal);
    return () => controller.abort();
  }, [load]);

  function openSession(id: string | null) {
    setActiveId(id);
    setSession((current) => ({ key: current.key + 1, conversationId: id }));
    showInAddressBar(id);
  }

  function open(id: string) {
    if (id === activeId) return;
    setNotice(null);
    openSession(id);
  }

  function saved(conversation: ConversationSummary) {
    setConversations((current) => upsertConversation(current, conversation));
    if (conversation.id !== activeId) {
      setActiveId(conversation.id);
      showInAddressBar(conversation.id);
    }
  }

  // The server saves a stopped answer when the request closes; the list shows it at once.
  function stopped(id: string, question: string) {
    setConversations((current) => {
      const conversation = current.find((c) => c.id === id);
      if (!conversation) return current;
      return upsertConversation(current, {
        ...conversation,
        title: conversation.questionCount === 0 && conversation.title === UNTITLED_TITLE ? titleFrom(question) : conversation.title,
        questionCount: conversation.questionCount + 1,
        updatedAt: new Date().toISOString(),
      });
    });
  }

  function discarded(id: string) {
    setConversations((current) => current.filter((c) => c.id !== id));
    setActiveId((current) => (current === id ? null : current));
    showInAddressBar(null);
  }

  function missing() {
    setNotice(t("saved.missing"));
    setConversations((current) => current.filter((c) => c.id !== session.conversationId));
    openSession(null);
  }

  async function attempt(action: () => Promise<void>): Promise<LocalizedError | null> {
    try {
      await action();
      return null;
    } catch (failure) {
      return asError(failure);
    }
  }

  const actions: SavedChatsActions = {
    createFolder: (name) =>
      attempt(async () => {
        const folder = await savedChats.createFolder(name);
        setFolders((current) => sortFolders([...current, folder]));
      }),
    renameFolder: (id, name) =>
      attempt(async () => {
        const folder = await savedChats.renameFolder(id, name);
        setFolders((current) => sortFolders(current.map((f) => (f.id === id ? folder : f))));
      }),
    deleteFolder: (id) =>
      attempt(async () => {
        await savedChats.deleteFolder(id);
        setFolders((current) => current.filter((f) => f.id !== id));
        // Its chats are kept, without a folder.
        setConversations((current) => current.map((c) => (c.folderId === id ? { ...c, folderId: null } : c)));
      }),
    renameConversation: (id, title) =>
      attempt(async () => {
        const conversation = await savedChats.renameConversation(id, title);
        setConversations((current) => current.map((c) => (c.id === id ? conversation : c)));
      }),
    moveConversation: (id, folderId) =>
      attempt(async () => {
        const conversation = await savedChats.moveConversation(id, folderId);
        setConversations((current) => current.map((c) => (c.id === id ? conversation : c)));
      }),
    deleteConversation: (id) =>
      attempt(async () => {
        await savedChats.deleteConversation(id);
        setConversations((current) => current.filter((c) => c.id !== id));
        if (id === activeId) openSession(null);
      }),
  };

  return (
    <div className={styles.workspace}>
      <ConversationsPanel
        className={styles.panel}
        state={listState}
        folders={folders}
        conversations={conversations}
        activeId={activeId}
        hrefOf={hrefOf}
        onOpen={open}
        onRetry={() => {
          setListState("loading");
          load();
        }}
        actions={actions}
      />
      <div className={styles.chat}>
        {notice && (
          <div className={styles.notice}>
            <SavedChatsNotice message={notice} onDismiss={() => setNotice(null)} />
          </div>
        )}
        <ChatView
          key={session.key}
          initialConversationId={session.conversationId}
          onSaved={saved}
          onStopped={stopped}
          onMissing={missing}
          onDiscarded={discarded}
          onNewConversation={() => {
            setNotice(null);
            openSession(null);
          }}
        />
      </div>
    </div>
  );
}
