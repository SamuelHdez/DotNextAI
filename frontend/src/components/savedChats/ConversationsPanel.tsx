"use client";

import {
  Body1,
  Button,
  Caption1,
  makeStyles,
  Menu,
  MenuItem,
  MenuList,
  MenuPopover,
  MenuTrigger,
  MessageBar,
  MessageBarActions,
  MessageBarBody,
  mergeClasses,
  Spinner,
  Subtitle2,
  Text,
  tokens,
} from "@fluentui/react-components";
import {
  Chat20Regular,
  Dismiss20Regular,
  Folder20Regular,
  FolderAdd20Regular,
  MoreHorizontal20Regular,
} from "@fluentui/react-icons";
import { useId, useState, type MouseEvent } from "react";
import { ConfirmDialog } from "@/components/savedChats/ConfirmDialog";
import { MoveDialog } from "@/components/savedChats/MoveDialog";
import { NameDialog } from "@/components/savedChats/NameDialog";
import type { LocalizedError } from "@/lib/chat/chatErrors";
import { groupByFolder, MAX_FOLDER_NAME_LENGTH, MAX_TITLE_LENGTH, UNTITLED_TITLE } from "@/lib/chat/savedChats";
import { useI18n } from "@/lib/i18n/I18nProvider";
import type { ConversationSummary, Folder } from "@/services/types";

const useStyles = makeStyles({
  panel: { display: "flex", flexDirection: "column", gap: tokens.spacingVerticalM, minWidth: 0 },
  header: { display: "flex", alignItems: "center", justifyContent: "space-between", gap: tokens.spacingHorizontalS },
  group: { display: "flex", flexDirection: "column", gap: tokens.spacingVerticalXXS },
  groupHeader: { display: "flex", alignItems: "center", gap: tokens.spacingHorizontalXS, color: tokens.colorNeutralForeground2 },
  groupName: { flexGrow: 1, minWidth: 0, margin: 0, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" },
  list: { display: "flex", flexDirection: "column", gap: tokens.spacingVerticalXXS, margin: 0, padding: 0, listStyle: "none" },
  nested: { paddingInlineStart: tokens.spacingHorizontalL },
  row: { display: "flex", alignItems: "center", gap: tokens.spacingHorizontalXXS },
  item: {
    flexGrow: 1,
    minWidth: 0,
    justifyContent: "flex-start",
    fontWeight: tokens.fontWeightRegular,
  },
  itemText: { overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" },
  active: { backgroundColor: tokens.colorNeutralBackground1Selected, fontWeight: tokens.fontWeightSemibold },
  muted: { color: tokens.colorNeutralForeground3 },
});

type Dialog =
  | { kind: "newFolder" }
  | { kind: "renameFolder"; folder: Folder }
  | { kind: "deleteFolder"; folder: Folder }
  | { kind: "renameChat"; conversation: ConversationSummary }
  | { kind: "moveChat"; conversation: ConversationSummary }
  | { kind: "deleteChat"; conversation: ConversationSummary };

/** Every action resolves with the error to show, or null when it worked. */
export interface SavedChatsActions {
  createFolder: (name: string) => Promise<LocalizedError | null>;
  renameFolder: (id: string, name: string) => Promise<LocalizedError | null>;
  deleteFolder: (id: string) => Promise<LocalizedError | null>;
  renameConversation: (id: string, title: string) => Promise<LocalizedError | null>;
  moveConversation: (id: string, folderId: string | null) => Promise<LocalizedError | null>;
  deleteConversation: (id: string) => Promise<LocalizedError | null>;
}

interface ConversationsPanelProps {
  state: "loading" | "ready" | "failed";
  folders: readonly Folder[];
  conversations: readonly ConversationSummary[];
  activeId: string | null;
  /** Link of a conversation, so it can also be opened in a new tab. */
  hrefOf: (id: string) => string;
  onOpen: (id: string) => void;
  onRetry: () => void;
  actions: SavedChatsActions;
  className?: string;
}

/**
 * Saved chats: the folders (one level) with their chats, then the chats without a folder, each with a menu to rename,
 * move or delete. Opening a chat is a link (`?c=<id>`) handled in place.
 */
export function ConversationsPanel({ state, folders, conversations, activeId, hrefOf, onOpen, onRetry, actions, className }: ConversationsPanelProps) {
  const styles = useStyles();
  const { t } = useI18n();
  const headingId = useId();
  // The dialog stays mounted while it closes (Fluent restores the page's accessibility tree on close, not on unmount);
  // each opening gets a new key so its fields start empty.
  const [dialog, setDialog] = useState<Dialog | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [dialogKey, setDialogKey] = useState(0);
  const show = (next: Dialog) => {
    setDialog(next);
    setDialogOpen(true);
    setDialogKey((key) => key + 1);
  };
  const close = () => setDialogOpen(false);
  const titleOf = (conversation: ConversationSummary) =>
    conversation.title === UNTITLED_TITLE ? t("saved.untitled") : conversation.title;
  const groups = groupByFolder(folders, conversations);

  function open(event: MouseEvent, id: string) {
    // Modified clicks keep the browser's behaviour (new tab, new window).
    if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey || event.button !== 0) return;
    event.preventDefault();
    onOpen(id);
  }

  function chatList(items: readonly ConversationSummary[], nested: boolean) {
    return (
      <ul className={mergeClasses(styles.list, nested && styles.nested)}>
        {items.map((conversation) => {
          const title = titleOf(conversation);
          const active = conversation.id === activeId;
          return (
            <li key={conversation.id} className={styles.row}>
              <Button
                as="a"
                href={hrefOf(conversation.id)}
                appearance="subtle"
                icon={<Chat20Regular />}
                className={mergeClasses(styles.item, active && styles.active)}
                aria-current={active ? "page" : undefined}
                onClick={(event) => open(event, conversation.id)}
              >
                <span className={styles.itemText}>{title}</span>
              </Button>
              <Menu>
                <MenuTrigger disableButtonEnhancement>
                  <Button appearance="transparent" size="small" icon={<MoreHorizontal20Regular />} aria-label={t("saved.options", { name: title })} />
                </MenuTrigger>
                <MenuPopover>
                  <MenuList>
                    <MenuItem onClick={() => show({ kind: "renameChat", conversation })}>{t("saved.rename")}</MenuItem>
                    <MenuItem onClick={() => show({ kind: "moveChat", conversation })}>{t("saved.move")}</MenuItem>
                    <MenuItem onClick={() => show({ kind: "deleteChat", conversation })}>{t("saved.delete")}</MenuItem>
                  </MenuList>
                </MenuPopover>
              </Menu>
            </li>
          );
        })}
      </ul>
    );
  }

  return (
    <nav aria-labelledby={headingId} className={mergeClasses(styles.panel, className)}>
      <div className={styles.header}>
        <Subtitle2 as="h2" id={headingId}>
          {t("saved.title")}
        </Subtitle2>
        <Button appearance="subtle" icon={<FolderAdd20Regular />} onClick={() => show({ kind: "newFolder" })} disabled={state !== "ready"}>
          {t("saved.newFolder")}
        </Button>
      </div>

      {state === "loading" && <Spinner size="tiny" label={t("saved.loading")} />}
      {state === "failed" && (
        <MessageBar intent="error">
          <MessageBarBody>{t("saved.loadFailed")}</MessageBarBody>
          <MessageBarActions>
            <Button onClick={onRetry}>{t("saved.retry")}</Button>
          </MessageBarActions>
        </MessageBar>
      )}

      {state === "ready" && (
        <>
          {groups.folders.map(({ folder, conversations: inFolder }) => (
            <section key={folder.id} className={styles.group} aria-labelledby={`${headingId}-${folder.id}`}>
              <div className={styles.groupHeader}>
                <Folder20Regular aria-hidden />
                <Text as="h3" weight="semibold" size={300} id={`${headingId}-${folder.id}`} className={styles.groupName}>
                  {folder.name}
                </Text>
                <Menu>
                  <MenuTrigger disableButtonEnhancement>
                    <Button
                      appearance="transparent"
                      size="small"
                      icon={<MoreHorizontal20Regular />}
                      aria-label={t("saved.folderOptions", { name: folder.name })}
                    />
                  </MenuTrigger>
                  <MenuPopover>
                    <MenuList>
                      <MenuItem onClick={() => show({ kind: "renameFolder", folder })}>{t("saved.rename")}</MenuItem>
                      <MenuItem onClick={() => show({ kind: "deleteFolder", folder })}>{t("saved.delete")}</MenuItem>
                    </MenuList>
                  </MenuPopover>
                </Menu>
              </div>
              {inFolder.length > 0 ? chatList(inFolder, true) : <Caption1 className={mergeClasses(styles.muted, styles.nested)}>{t("saved.folderEmpty")}</Caption1>}
            </section>
          ))}

          {(groups.unfiled.length > 0 || groups.folders.length === 0) && (
            <section className={styles.group} aria-labelledby={`${headingId}-unfiled`}>
              {groups.folders.length > 0 && (
                <div className={styles.groupHeader}>
                  <Text as="h3" weight="semibold" size={300} id={`${headingId}-unfiled`} className={styles.groupName}>
                    {t("saved.unfiled")}
                  </Text>
                </div>
              )}
              {groups.unfiled.length > 0 ? (
                chatList(groups.unfiled, false)
              ) : (
                <Body1 className={styles.muted}>{t("saved.empty")}</Body1>
              )}
            </section>
          )}
        </>
      )}

      {dialog?.kind === "newFolder" && (
        <NameDialog
          key={dialogKey}
          open={dialogOpen}
          title={t("saved.newFolder")}
          label={t("saved.folder.label")}
          submitLabel={t("saved.create")}
          max={MAX_FOLDER_NAME_LENGTH}
          onSubmit={actions.createFolder}
          onClose={close}
        />
      )}
      {dialog?.kind === "renameFolder" && (
        <NameDialog
          key={dialogKey}
          open={dialogOpen}
          title={t("saved.renameFolder")}
          label={t("saved.folder.label")}
          submitLabel={t("saved.save")}
          initialValue={dialog.folder.name}
          max={MAX_FOLDER_NAME_LENGTH}
          onSubmit={(name) => actions.renameFolder(dialog.folder.id, name)}
          onClose={close}
        />
      )}
      {dialog?.kind === "deleteFolder" && (
        <ConfirmDialog
          key={dialogKey}
          open={dialogOpen}
          title={t("saved.deleteFolder")}
          body={t("saved.deleteFolder.body", { name: dialog.folder.name })}
          confirmLabel={t("saved.deleteConfirm")}
          onConfirm={() => actions.deleteFolder(dialog.folder.id)}
          onClose={close}
        />
      )}
      {dialog?.kind === "renameChat" && (
        <NameDialog
          key={dialogKey}
          open={dialogOpen}
          title={t("saved.renameChat")}
          label={t("saved.title.label")}
          submitLabel={t("saved.save")}
          initialValue={titleOf(dialog.conversation)}
          max={MAX_TITLE_LENGTH}
          onSubmit={(title) => actions.renameConversation(dialog.conversation.id, title)}
          onClose={close}
        />
      )}
      {dialog?.kind === "moveChat" && (
        <MoveDialog
          key={dialogKey}
          open={dialogOpen}
          chatTitle={titleOf(dialog.conversation)}
          folders={folders}
          currentFolderId={dialog.conversation.folderId}
          onMove={(folderId) => actions.moveConversation(dialog.conversation.id, folderId)}
          onClose={close}
        />
      )}
      {dialog?.kind === "deleteChat" && (
        <ConfirmDialog
          key={dialogKey}
          open={dialogOpen}
          title={t("saved.deleteChat")}
          body={t("saved.deleteChat.body", { name: titleOf(dialog.conversation) })}
          confirmLabel={t("saved.deleteConfirm")}
          onConfirm={() => actions.deleteConversation(dialog.conversation.id)}
          onClose={close}
        />
      )}
    </nav>
  );
}

/** A dismissable notice above the chat (e.g. a saved chat that no longer exists). */
export function SavedChatsNotice({ message, onDismiss }: { message: string; onDismiss: () => void }) {
  const { t } = useI18n();
  return (
    <MessageBar intent="info">
      <MessageBarBody>{message}</MessageBarBody>
      <MessageBarActions
        containerAction={<Button appearance="transparent" icon={<Dismiss20Regular />} aria-label={t("chat.error.dismiss")} onClick={onDismiss} />}
      />
    </MessageBar>
  );
}
