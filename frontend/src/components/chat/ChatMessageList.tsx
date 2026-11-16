"use client";

import { Caption1, makeStyles, mergeClasses, Spinner, Text, tokens } from "@fluentui/react-components";
import { Warning16Regular } from "@fluentui/react-icons";
import { useEffect, useRef } from "react";
import type { ChatMessage } from "@/lib/chat/conversation";
import { useI18n } from "@/lib/i18n/I18nProvider";

const useStyles = makeStyles({
  log: { display: "flex", flexDirection: "column", gap: tokens.spacingVerticalL, margin: 0, padding: 0, listStyle: "none" },
  row: { display: "flex", flexDirection: "column", gap: tokens.spacingVerticalXXS, maxWidth: "min(760px, 90%)" },
  userRow: { alignSelf: "flex-end", alignItems: "flex-end" },
  assistantRow: { alignSelf: "flex-start", alignItems: "flex-start" },
  meta: { display: "flex", gap: tokens.spacingHorizontalS, alignItems: "baseline", color: tokens.colorNeutralForeground3 },
  bubble: {
    padding: `${tokens.spacingVerticalM} ${tokens.spacingHorizontalL}`,
    borderRadius: tokens.borderRadiusXLarge,
    // Answers are plain text for now: keep the model's line breaks and break long words or URLs.
    whiteSpace: "pre-wrap",
    overflowWrap: "anywhere",
    lineHeight: tokens.lineHeightBase400,
  },
  userBubble: {
    backgroundColor: tokens.colorBrandBackground,
    color: tokens.colorNeutralForegroundOnBrand,
    borderBottomRightRadius: tokens.borderRadiusSmall,
  },
  assistantBubble: {
    backgroundColor: tokens.colorNeutralBackground1,
    color: tokens.colorNeutralForeground1,
    border: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralStroke2}`,
    borderBottomLeftRadius: tokens.borderRadiusSmall,
  },
  truncated: { display: "flex", alignItems: "center", gap: tokens.spacingHorizontalXS, color: tokens.colorPaletteMarigoldForeground2 },
});

interface ChatMessageListProps {
  messages: readonly ChatMessage[];
  /** Question being answered right now, shown with a "thinking" indicator. */
  pendingQuestion?: string | null;
}

/** The conversation, oldest first. New messages are announced by screen readers (role="log") and scrolled into view. */
export function ChatMessageList({ messages, pendingQuestion }: ChatMessageListProps) {
  const styles = useStyles();
  const { t } = useI18n();
  const end = useRef<HTMLLIElement>(null);

  useEffect(() => {
    end.current?.scrollIntoView?.({ block: "end", behavior: "smooth" });
  }, [messages.length, pendingQuestion]);

  return (
    <ol className={styles.log} role="log" aria-label={t("chat.history")} aria-live="polite">
      {messages.map((message) => {
        const isUser = message.role === "user";
        return (
          <li key={message.id} className={mergeClasses(styles.row, isUser ? styles.userRow : styles.assistantRow)}>
            <div className={styles.meta}>
              <Text weight="semibold" size={200}>
                {isUser ? t("chat.you") : t("chat.assistant")}
              </Text>
              {message.model && <Caption1>{message.model}</Caption1>}
            </div>
            <div className={mergeClasses(styles.bubble, isUser ? styles.userBubble : styles.assistantBubble)}>{message.text}</div>
            {message.truncated && (
              <Caption1 className={styles.truncated}>
                <Warning16Regular aria-hidden />
                {t("chat.truncated")}
              </Caption1>
            )}
          </li>
        );
      })}
      {pendingQuestion && (
        <>
          <li className={mergeClasses(styles.row, styles.userRow)}>
            <div className={styles.meta}>
              <Text weight="semibold" size={200}>
                {t("chat.you")}
              </Text>
            </div>
            <div className={mergeClasses(styles.bubble, styles.userBubble)}>{pendingQuestion}</div>
          </li>
          <li className={mergeClasses(styles.row, styles.assistantRow)}>
            <Spinner size="tiny" label={t("chat.thinking")} />
          </li>
        </>
      )}
      <li ref={end} aria-hidden />
    </ol>
  );
}
