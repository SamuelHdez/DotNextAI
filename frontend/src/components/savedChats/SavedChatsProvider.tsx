"use client";

import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { chatErrorMessage, type LocalizedError } from "@/lib/chat/chatErrors";
import { sortFolders, titleFrom, UNTITLED_TITLE, upsertConversation } from "@/lib/chat/savedChats";
import * as savedChats from "@/services/conversationsService";
import type { ConversationSummary, Folder } from "@/services/types";

/** Every action resolves with the error to show, or null when it worked. */
export interface SavedChatsActions {
  createFolder: (name: string) => Promise<LocalizedError | null>;
  renameFolder: (id: string, name: string) => Promise<LocalizedError | null>;
  deleteFolder: (id: string) => Promise<LocalizedError | null>;
  renameConversation: (id: string, title: string) => Promise<LocalizedError | null>;
  moveConversation: (id: string, folderId: string | null) => Promise<LocalizedError | null>;
  deleteConversation: (id: string) => Promise<LocalizedError | null>;
}

/** What the chat page lets the navigation do while it is shown. */
export interface ChatPageHandle {
  /** Opens a saved chat in place. */
  open: (id: string) => void;
  /** Starts a new conversation (the open one was deleted). */
  reset: () => void;
}

interface SavedChatsContextValue {
  state: "loading" | "ready" | "failed";
  folders: Folder[];
  conversations: ConversationSummary[];
  /** The chat open on the chat page; null elsewhere or for a new conversation. */
  activeId: string | null;
  setActiveId: (id: string | null) => void;
  retry: () => void;
  actions: SavedChatsActions;
  /** The conversation as the server saved it (created or answered). */
  upsert: (conversation: ConversationSummary) => void;
  /** A stopped answer was saved with the text so far: the list shows it at once (there is no `done` event). */
  markStopped: (id: string, question: string) => void;
  remove: (id: string) => void;
  /** Opens a chat on the chat page if it is shown; false when the caller must navigate to `/chat?c=<id>`. */
  openInPlace: (id: string) => boolean;
  registerChatPage: (handle: ChatPageHandle) => () => void;
}

const SavedChatsContext = createContext<SavedChatsContextValue | null>(null);

/** The saved chats, or null outside the provider (e.g. isolated component tests). */
export function useSavedChats(): SavedChatsContextValue | null {
  return useContext(SavedChatsContext);
}

async function attempt(action: () => Promise<void>): Promise<LocalizedError | null> {
  try {
    await action();
    return null;
  } catch (failure) {
    return chatErrorMessage(failure);
  }
}

/**
 * The saved chats and folders (docs/specs/feature-saved-chats.md), shared by the side navigation, which lists them,
 * and the chat page, which saves into them. Lives in the app shell, so the list is loaded once and kept across pages.
 */
export function SavedChatsProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<"loading" | "ready" | "failed">("loading");
  const [folders, setFolders] = useState<Folder[]>([]);
  const [conversations, setConversations] = useState<ConversationSummary[]>([]);
  const [activeId, setActiveIdState] = useState<string | null>(null);
  const activeRef = useRef<string | null>(null);
  const chatPage = useRef<ChatPageHandle | null>(null);

  // The list starts as "loading"; a retry sets it again before calling this.
  const load = useCallback((signal?: AbortSignal) => {
    Promise.all([savedChats.listFolders(signal), savedChats.listConversations(signal)])
      .then(([loadedFolders, loadedConversations]) => {
        setFolders(sortFolders(loadedFolders));
        setConversations(loadedConversations);
        setState("ready");
      })
      .catch(() => {
        if (!signal?.aborted) setState("failed");
      });
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    load(controller.signal);
    return () => controller.abort();
  }, [load]);

  // Stable, so the chat page can register once per visit.
  const setActiveId = useCallback((id: string | null) => {
    activeRef.current = id;
    setActiveIdState(id);
  }, []);
  const registerChatPage = useCallback((handle: ChatPageHandle) => {
    chatPage.current = handle;
    return () => {
      if (chatPage.current === handle) chatPage.current = null;
    };
  }, []);

  const value = useMemo<SavedChatsContextValue>(() => {
    return {
      state,
      folders,
      conversations,
      activeId,
      setActiveId,
      retry: () => {
        setState("loading");
        load();
      },
      upsert: (conversation) => setConversations((current) => upsertConversation(current, conversation)),
      markStopped: (id, question) =>
        setConversations((current) => {
          const conversation = current.find((c) => c.id === id);
          if (!conversation) return current;
          return upsertConversation(current, {
            ...conversation,
            title: conversation.questionCount === 0 && conversation.title === UNTITLED_TITLE ? titleFrom(question) : conversation.title,
            questionCount: conversation.questionCount + 1,
            updatedAt: new Date().toISOString(),
          });
        }),
      remove: (id) => setConversations((current) => current.filter((c) => c.id !== id)),
      openInPlace: (id) => {
        if (!chatPage.current) return false;
        chatPage.current.open(id);
        return true;
      },
      registerChatPage,
      actions: {
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
            if (id === activeRef.current) chatPage.current?.reset();
          }),
      },
    };
  }, [state, folders, conversations, activeId, load, setActiveId, registerChatPage]);

  return <SavedChatsContext.Provider value={value}>{children}</SavedChatsContext.Provider>;
}
