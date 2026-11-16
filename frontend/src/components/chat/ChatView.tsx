"use client";

import {
  Body1,
  Button,
  makeStyles,
  MessageBar,
  MessageBarActions,
  MessageBarBody,
  Subtitle1,
  tokens,
} from "@fluentui/react-components";
import { ArrowCounterclockwise20Regular, Dismiss20Regular, Sparkle24Regular } from "@fluentui/react-icons";
import { useEffect, useRef, useState } from "react";
import { ChatComposer } from "@/components/chat/ChatComposer";
import { ChatMessageList } from "@/components/chat/ChatMessageList";
import { PageBody, PageHeader } from "@/components/ui/PageLayout";
import { chatErrorMessage, type LocalizedError } from "@/lib/chat/chatErrors";
import { checkQuestion, MAX_CONVERSATION_QUESTIONS, MAX_QUESTION_LENGTH } from "@/lib/chat/chatRules";
import { questionCount, toHistory, type ChatMessage } from "@/lib/chat/conversation";
import { useI18n } from "@/lib/i18n/I18nProvider";
import type { MessageKey } from "@/lib/i18n/messages/en";
import { sendChat } from "@/services/chatService";

const SUGGESTIONS: MessageKey[] = ["chat.suggestion.1", "chat.suggestion.2", "chat.suggestion.3"];

const useStyles = makeStyles({
  page: { gap: tokens.spacingVerticalL },
  conversation: { flexGrow: 1, display: "flex", flexDirection: "column", gap: tokens.spacingVerticalL },
  empty: {
    display: "flex",
    flexDirection: "column",
    alignItems: "center",
    textAlign: "center",
    gap: tokens.spacingVerticalM,
    paddingBlock: tokens.spacingVerticalXXXL,
  },
  emptyIcon: { color: tokens.colorBrandForeground1, fontSize: "32px" },
  emptyText: { color: tokens.colorNeutralForeground3 },
  suggestions: { display: "flex", flexWrap: "wrap", justifyContent: "center", gap: tokens.spacingHorizontalS },
  // The composer stays reachable at the bottom of the window while the conversation scrolls.
  composer: {
    position: "sticky",
    bottom: 0,
    display: "flex",
    flexDirection: "column",
    gap: tokens.spacingVerticalS,
    paddingBlock: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground2,
  },
});

/**
 * Backlog step 05: the chat in the browser. The conversation lives in this component's state (one tab, lost on
 * reload) and is sent with every question to POST /api/chat, which keeps no state.
 */
export function ChatView() {
  const styles = useStyles();
  const { t } = useI18n();
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [draft, setDraft] = useState("");
  const [pendingQuestion, setPendingQuestion] = useState<string | null>(null);
  const [error, setError] = useState<LocalizedError | null>(null);
  const [inputError, setInputError] = useState<LocalizedError | null>(null);
  const request = useRef<AbortController | null>(null);
  const nextId = useRef(0);

  // Leaving the page cancels the question in flight.
  useEffect(() => () => request.current?.abort(), []);

  const full = questionCount(messages) >= MAX_CONVERSATION_QUESTIONS;

  async function ask(text: string) {
    if (pendingQuestion || full) return;
    const check = checkQuestion(text);
    if (!check.ok) {
      setInputError(
        check.reason === "empty"
          ? { key: "question.empty" }
          : { key: "question.tooLong", params: { count: check.length, max: MAX_QUESTION_LENGTH } },
      );
      return;
    }

    setInputError(null);
    setError(null);
    setDraft("");
    setPendingQuestion(check.question);
    const controller = new AbortController();
    request.current = controller;

    try {
      const response = await sendChat({ question: check.question, history: toHistory(messages) }, controller.signal);
      const id = nextId.current;
      nextId.current += 2;
      setMessages((current) => [
        ...current,
        { id: `m${id}`, role: "user", text: check.question },
        { id: `m${id + 1}`, role: "assistant", text: response.answer, model: response.model, truncated: response.truncated },
      ]);
    } catch (failure) {
      if (controller.signal.aborted) return;
      setError(chatErrorMessage(failure));
      // Give the question back so it can be sent again (unless the user already typed something else).
      setDraft((current) => current || text);
    } finally {
      if (request.current === controller) {
        request.current = null;
        setPendingQuestion(null);
      }
    }
  }

  function newConversation() {
    request.current?.abort();
    request.current = null;
    setPendingQuestion(null);
    setMessages([]);
    setError(null);
    setInputError(null);
  }

  const isEmpty = messages.length === 0 && !pendingQuestion;

  return (
    <PageBody className={styles.page}>
      <PageHeader
        title={t("chat.title")}
        subtitle={t("chat.subtitle")}
        actions={
          <Button icon={<ArrowCounterclockwise20Regular />} onClick={newConversation} disabled={isEmpty}>
            {t("chat.new")}
          </Button>
        }
      />

      <div className={styles.conversation}>
        {isEmpty ? (
          <div className={styles.empty}>
            <Sparkle24Regular className={styles.emptyIcon} aria-hidden />
            <Subtitle1 as="h2">{t("chat.empty.title")}</Subtitle1>
            <Body1 className={styles.emptyText}>{t("chat.empty.description")}</Body1>
            <div className={styles.suggestions}>
              {SUGGESTIONS.map((key) => (
                <Button key={key} appearance="outline" shape="circular" onClick={() => ask(t(key))}>
                  {t(key)}
                </Button>
              ))}
            </div>
          </div>
        ) : (
          <ChatMessageList messages={messages} pendingQuestion={pendingQuestion} />
        )}
      </div>

      <div className={styles.composer}>
        {error && (
          <MessageBar intent="error">
            <MessageBarBody>{t(error.key, error.params)}</MessageBarBody>
            <MessageBarActions
              containerAction={
                <Button appearance="transparent" icon={<Dismiss20Regular />} aria-label={t("chat.error.dismiss")} onClick={() => setError(null)} />
              }
            />
          </MessageBar>
        )}
        {full && (
          <MessageBar intent="warning">
            <MessageBarBody>{t("chat.limit", { max: MAX_CONVERSATION_QUESTIONS })}</MessageBarBody>
          </MessageBar>
        )}
        <ChatComposer
          value={draft}
          onChange={(value) => {
            setDraft(value);
            if (inputError) setInputError(null);
          }}
          onSubmit={() => ask(draft)}
          busy={pendingQuestion !== null}
          disabled={full}
          validationMessage={inputError ? t(inputError.key, inputError.params) : undefined}
        />
      </div>
    </PageBody>
  );
}
