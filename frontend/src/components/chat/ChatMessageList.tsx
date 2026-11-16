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
  stopped: { color: tokens.colorNeutralForeground3 },
  images: { display: "flex", flexWrap: "wrap", justifyContent: "flex-end", gap: tokens.spacingHorizontalS },
  image: {
    maxWidth: "200px",
    maxHeight: "160px",
    borderRadius: tokens.borderRadiusLarge,
    border: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralStroke2}`,
    objectFit: "cover",
  },
});

function Images({ images, className, imageClassName }: { images?: { name: string; src: string }[]; className: string; imageClassName: string }) {
  const { t } = useI18n();
  if (!images?.length) return null;
  return (
    <div className={className}>
      {images.map((image, index) => (
        // eslint-disable-next-line @next/next/no-img-element -- local data: URI of an attached image, nothing to optimise
        <img key={index} src={image.src} alt={t("chat.image.alt", { name: image.name })} className={imageClassName} />
      ))}
    </div>
  );
}

interface ChatMessageListProps {
  messages: readonly ChatMessage[];
  /** Question being answered right now, shown with a "thinking" indicator. */
  pendingQuestion?: string | null;
  /** Images sent with the pending question. */
  pendingImages?: { name: string; src: string }[];
  /** The answer to the pending question so far (step 07); empty until the first piece arrives. */
  pendingAnswer?: string;
}

/** The conversation, oldest first. New messages are announced by screen readers (role="log") and scrolled into view. */
export function ChatMessageList({ messages, pendingQuestion, pendingImages, pendingAnswer = "" }: ChatMessageListProps) {
  const styles = useStyles();
  const { t } = useI18n();
  const end = useRef<HTMLLIElement>(null);

  useEffect(() => {
    end.current?.scrollIntoView?.({ block: "end", behavior: "smooth" });
  }, [messages.length, pendingQuestion, pendingAnswer]);

  return (
    // The live region wraps the list: putting role="log" on the <ol> itself would drop its list semantics.
    // aria-busy while an answer streams: screen readers announce it once, complete, instead of every piece.
    <div role="log" aria-label={t("chat.history")} aria-live="polite" aria-busy={pendingQuestion ? true : undefined}>
      <ol className={styles.log}>
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
              <Images images={message.images} className={styles.images} imageClassName={styles.image} />
              <div className={mergeClasses(styles.bubble, isUser ? styles.userBubble : styles.assistantBubble)}>{message.text}</div>
              {message.truncated && (
                <Caption1 className={styles.truncated}>
                  <Warning16Regular aria-hidden />
                  {t("chat.truncated")}
                </Caption1>
              )}
              {message.stopped && <Caption1 className={styles.stopped}>{t("chat.stopped")}</Caption1>}
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
              <Images images={pendingImages} className={styles.images} imageClassName={styles.image} />
              <div className={mergeClasses(styles.bubble, styles.userBubble)}>{pendingQuestion}</div>
            </li>
            <li className={mergeClasses(styles.row, styles.assistantRow)}>
              {pendingAnswer ? (
                <>
                  <div className={styles.meta}>
                    <Text weight="semibold" size={200}>
                      {t("chat.assistant")}
                    </Text>
                  </div>
                  <div className={mergeClasses(styles.bubble, styles.assistantBubble)} data-streaming>
                    {pendingAnswer}
                  </div>
                </>
              ) : (
                <Spinner size="tiny" label={t("chat.thinking")} />
              )}
            </li>
          </>
        )}
        <li ref={end} aria-hidden />
      </ol>
    </div>
  );
}
