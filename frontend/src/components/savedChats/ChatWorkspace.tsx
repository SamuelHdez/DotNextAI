"use client";

import { makeStyles, tokens } from "@fluentui/react-components";
import { useEffect, useEffectEvent, useState } from "react";
import { ChatView } from "@/components/chat/ChatView";
import { SavedChatsNotice } from "@/components/savedChats/SavedChatsSection";
import { useSavedChats } from "@/components/savedChats/SavedChatsProvider";
import { chatHref } from "@/lib/chat/savedChats";
import { useI18n } from "@/lib/i18n/I18nProvider";
import type { ConversationSummary } from "@/services/types";

const useStyles = makeStyles({
  chat: { flexGrow: 1, minWidth: 0, display: "flex", flexDirection: "column" },
  notice: { width: "100%", maxWidth: "1200px", marginInline: "auto", boxSizing: "border-box", padding: `${tokens.spacingVerticalM} ${tokens.spacingHorizontalXXL} 0` },
});

/** The open conversation in the address bar, so a reload or a shared link reopens it (no Next.js navigation). */
function showInAddressBar(id: string | null) {
  window.history.replaceState(window.history.state, "", id ? chatHref(id) : "/chat");
}

/**
 * The chat page with saved chats (docs/specs/feature-saved-chats.md). The list lives in the side navigation
 * (`SavedChatsProvider`); this page tells it which chat is open and opens the chats chosen there in place. Opening a
 * chat or starting a new one remounts the chat (`session`); a chat that saves itself for the first time only updates
 * the list and the address bar.
 */
export function ChatWorkspace({ initialConversationId = null }: { initialConversationId?: string | null }) {
  const styles = useStyles();
  const { t } = useI18n();
  const saved = useSavedChats();
  const [session, setSession] = useState({ key: 0, conversationId: initialConversationId });
  const [notice, setNotice] = useState<string | null>(null);

  function openSession(id: string | null) {
    setSession((current) => ({ key: current.key + 1, conversationId: id }));
    saved?.setActiveId(id);
    showInAddressBar(id);
  }

  const openFromNavigation = useEffectEvent((id: string) => {
    setNotice(null);
    openSession(id);
  });
  const resetFromNavigation = useEffectEvent(() => openSession(null));

  // While the page is shown, the navigation opens chats here and marks the open one.
  const setActiveId = saved?.setActiveId;
  const registerChatPage = saved?.registerChatPage;
  useEffect(() => {
    if (!setActiveId || !registerChatPage) return;
    setActiveId(initialConversationId);
    const unregister = registerChatPage({ open: (id) => openFromNavigation(id), reset: () => resetFromNavigation() });
    return () => {
      unregister();
      setActiveId(null);
    };
  }, [setActiveId, registerChatPage, initialConversationId]);

  function onSaved(conversation: ConversationSummary) {
    saved?.upsert(conversation);
    if (conversation.id !== saved?.activeId) {
      saved?.setActiveId(conversation.id);
      showInAddressBar(conversation.id);
    }
  }

  function onDiscarded(id: string) {
    saved?.remove(id);
    saved?.setActiveId(null);
    showInAddressBar(null);
  }

  function onMissing() {
    setNotice(t("saved.missing"));
    if (session.conversationId) saved?.remove(session.conversationId);
    openSession(null);
  }

  return (
    <div className={styles.chat}>
      {notice && (
        <div className={styles.notice}>
          <SavedChatsNotice message={notice} onDismiss={() => setNotice(null)} />
        </div>
      )}
      <ChatView
        key={session.key}
        initialConversationId={session.conversationId}
        onSaved={onSaved}
        onStopped={(id, question) => saved?.markStopped(id, question)}
        onMissing={onMissing}
        onDiscarded={onDiscarded}
        onNewConversation={() => {
          setNotice(null);
          openSession(null);
        }}
      />
    </div>
  );
}
