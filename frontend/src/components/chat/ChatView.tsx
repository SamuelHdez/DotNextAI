"use client";

import {
  Body1,
  Button,
  Caption1,
  makeStyles,
  MessageBar,
  MessageBarActions,
  MessageBarBody,
  Spinner,
  Subtitle1,
  tokens,
} from "@fluentui/react-components";
import { ArrowCounterclockwise20Regular, Dismiss20Regular, Sparkle24Regular } from "@fluentui/react-icons";
import { useEffect, useEffectEvent, useRef, useState } from "react";
import { ChatComposer } from "@/components/chat/ChatComposer";
import { ChatMessageList } from "@/components/chat/ChatMessageList";
import { ModelPicker, type ModelList } from "@/components/chat/ModelPicker";
import { PageHeader } from "@/components/ui/PageLayout";
import { chatErrorMessage, type LocalizedError } from "@/lib/chat/chatErrors";
import { checkQuestion, MAX_CONVERSATION_QUESTIONS, MAX_QUESTION_LENGTH } from "@/lib/chat/chatRules";
import { MAX_IMAGE_MEGABYTES, MAX_IMAGES_PER_QUESTION, readImageFile, type ImageAttachment } from "@/lib/chat/images";
import { conversationUsage, questionCount, type ChatMessage } from "@/lib/chat/conversation";
import { formatCost } from "@/lib/chat/cost";
import { initialModel, readModelPreference, storeModelPreference } from "@/lib/chat/modelPreference";
import { toChatMessages } from "@/lib/chat/savedChats";
import { useI18n } from "@/lib/i18n/I18nProvider";
import type { MessageKey } from "@/lib/i18n/messages/en";
import { ApiError, apiUrl } from "@/services/apiClient";
import { createConversation, deleteConversation, getConversation, streamConversation } from "@/services/conversationsService";
import { getModels } from "@/services/modelsService";
import type { ConversationSummary } from "@/services/types";

const SUGGESTIONS: MessageKey[] = ["chat.suggestion.1", "chat.suggestion.2", "chat.suggestion.3"];

// The same centred column as PageBody, for each of the three bands of the chat.
const column = {
  width: "100%",
  maxWidth: "1200px",
  marginInline: "auto",
  boxSizing: "border-box",
  paddingInline: tokens.spacingHorizontalXXL,
  "@media (max-width: 639px)": { paddingInline: tokens.spacingHorizontalL },
} as const;

const useStyles = makeStyles({
  // Header, conversation and composer fill the space they get (ChatWorkspace gives the screen's height); only the
  // conversation scrolls, so the title, the model and the composer never move.
  root: { flexGrow: 1, minHeight: 0, display: "flex", flexDirection: "column" },
  top: { ...column, flexShrink: 0, paddingTop: tokens.spacingVerticalXXL, paddingBottom: tokens.spacingVerticalL },
  scroller: { flexGrow: 1, minHeight: 0, overflowY: "auto", overscrollBehavior: "contain" },
  bottom: {
    flexShrink: 0,
    borderTop: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralStroke2}`,
    backgroundColor: tokens.colorNeutralBackground2,
  },
  usage: { display: "flex", flexDirection: "column", color: tokens.colorNeutralForeground2 },
  usageHint: { color: tokens.colorNeutralForeground3 },
  actions: { display: "flex", alignItems: "center", flexWrap: "wrap", gap: tokens.spacingHorizontalM },
  conversation: { ...column, display: "flex", flexDirection: "column", gap: tokens.spacingVerticalL, paddingBlock: tokens.spacingVerticalL },
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
  composer: {
    ...column,
    display: "flex",
    flexDirection: "column",
    gap: tokens.spacingVerticalS,
    paddingBlock: tokens.spacingVerticalM,
  },
});

interface ChatViewProps {
  /** A saved conversation to open; without one, the chat is saved when its first question is asked. Read on mount. */
  initialConversationId?: string | null;
  /** The conversation as saved: after it is created and after every complete answer. */
  onSaved?: (conversation: ConversationSummary) => void;
  /** An answer was stopped: the server saved the question with the text so far. */
  onStopped?: (conversationId: string, question: string) => void;
  /** The conversation to open does not exist (any more). */
  onMissing?: () => void;
  /** The conversation created for a first question was deleted again, because nothing was saved in it. */
  onDiscarded?: (conversationId: string) => void;
  /** "New conversation" was pressed; without it the view empties itself. */
  onNewConversation?: () => void;
}

/**
 * Backlog step 05: the chat in the browser; step 07 streams the answer with a Stop button. Since the saved chats
 * (docs/specs/feature-saved-chats.md) every conversation is saved: the first question creates it
 * (POST /api/conversations) and every question goes to POST /api/conversations/{id}/messages/stream, where the server
 * reads the history from the database. The messages are also kept in this component's state for display.
 */
export function ChatView({ initialConversationId = null, onSaved, onStopped, onMissing, onDiscarded, onNewConversation }: ChatViewProps = {}) {
  const styles = useStyles();
  const { t, locale } = useI18n();
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [draft, setDraft] = useState("");
  const [pendingQuestion, setPendingQuestion] = useState<string | null>(null);
  const [error, setError] = useState<LocalizedError | null>(null);
  const [inputError, setInputError] = useState<LocalizedError | null>(null);
  const [attachments, setAttachments] = useState<ImageAttachment[]>([]);
  const [pendingImages, setPendingImages] = useState<{ name: string; src: string }[]>([]);
  // The answer so far, in a ref for Stop (state updates are batched) and in state for rendering.
  const [pendingAnswer, setPendingAnswer] = useState("");
  const answerSoFar = useRef("");
  const [modelList, setModelList] = useState<ModelList>({ state: "loading" });
  const [model, setModel] = useState<string | null>(null);
  const request = useRef<{ controller: AbortController; question: string; text: string; images: ImageAttachment[]; created: boolean } | null>(null);
  const nextId = useRef(0);
  // The saved conversation of this chat; null until its first question creates it.
  const conversationId = useRef<string | null>(initialConversationId);
  const [opening, setOpening] = useState(initialConversationId !== null);
  // Set when the page is unloading (reload, closing the tab): requests then fail as network errors, which says nothing
  // about what the server saved, so nothing may be deleted.
  const leaving = useRef(false);
  useEffect(() => {
    const onPageHide = () => {
      leaving.current = true;
    };
    window.addEventListener("pagehide", onPageHide);
    return () => window.removeEventListener("pagehide", onPageHide);
  }, []);

  // Not a dependency of the effect below: it runs once per conversation to open.
  const reportMissing = useEffectEvent(() => onMissing?.());

  // Leaving the page (or opening another chat) cancels the question in flight; a chat created for it that got no text
  // is deleted, since nothing will be saved in it.
  useEffect(
    () => () => {
      const current = request.current;
      if (!current) return;
      current.controller.abort();
      if (current.created && !answerSoFar.current.trim() && conversationId.current) {
        void deleteConversation(conversationId.current).catch(() => undefined);
      }
    },
    [],
  );

  // Opening a saved conversation: its messages, with the images served by the API.
  useEffect(() => {
    if (!initialConversationId) return;
    const controller = new AbortController();
    getConversation(initialConversationId, controller.signal)
      .then((detail) => setMessages(toChatMessages(detail, apiUrl)))
      .catch((failure) => {
        if (controller.signal.aborted) return;
        if (failure instanceof ApiError && failure.status === 404) {
          conversationId.current = null;
          reportMissing();
        } else {
          setError(chatErrorMessage(failure));
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setOpening(false);
      });
    return () => controller.abort();
  }, [initialConversationId]);

  // The model list (step 08). If it cannot be loaded the selector is disabled and the server's default answers.
  useEffect(() => {
    const controller = new AbortController();
    getModels(controller.signal)
      .then(({ models, defaultModel }) => {
        setModelList({ state: "ready", models, defaultModel });
        setModel(initialModel(models.map((m) => m.id), defaultModel, readModelPreference()));
      })
      .catch(() => {
        if (!controller.signal.aborted) setModelList({ state: "failed" });
      });
    return () => controller.abort();
  }, []);

  function chooseModel(next: string) {
    setModel(next);
    if (modelList.state === "ready") storeModelPreference(next, modelList.defaultModel);
  }

  // Only a model other than the server's default is sent.
  const requestedModel = modelList.state === "ready" && model && model !== modelList.defaultModel ? model : undefined;

  const full = questionCount(messages) >= MAX_CONVERSATION_QUESTIONS;

  async function ask(text: string) {
    if (pendingQuestion || full || opening) return;
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
    const sentImages = attachments;
    setAttachments([]);
    setPendingImages(sentImages.map(({ name, previewUrl }) => ({ name, src: previewUrl })));
    answerSoFar.current = "";
    setPendingAnswer("");
    const controller = new AbortController();
    request.current = { controller, question: check.question, text, images: sentImages, created: false };

    try {
      let savedId = conversationId.current;
      if (!savedId) {
        const created = await createConversation(null, controller.signal);
        savedId = created.id;
        conversationId.current = savedId;
        if (request.current?.controller === controller) request.current.created = true;
        onSaved?.(created);
      }
      const response = await streamConversation(
        savedId,
        {
          question: check.question,
          ...(sentImages.length > 0 && { images: sentImages.map(({ mediaType, data }) => ({ mediaType, data })) }),
          ...(requestedModel && { model: requestedModel }),
        },
        (piece) => {
          answerSoFar.current += piece;
          setPendingAnswer(answerSoFar.current);
        },
        controller.signal,
      );
      if (response.conversation) onSaved?.(response.conversation);
      const id = nextId.current;
      nextId.current += 2;
      setMessages((current) => [
        ...current,
        {
          id: `m${id}`,
          role: "user",
          text: check.question,
          images: sentImages.length > 0 ? sentImages.map(({ name, previewUrl }) => ({ name, src: previewUrl })) : undefined,
        },
        {
          id: `m${id + 1}`,
          role: "assistant",
          text: response.answer,
          model: response.model,
          truncated: response.truncated,
          ...(response.usage && { usage: response.usage }),
        },
      ]);
    } catch (failure) {
      if (controller.signal.aborted) return;
      // A conversation created for this question stays empty when the server saved nothing: no text arrived, or the
      // answer broke with an `error` event. After text, a dropped connection is saved by the server as stopped.
      const savedNothing = !answerSoFar.current.trim() || (failure instanceof ApiError && !failure.isNetworkError);
      if (request.current?.controller === controller && request.current.created && savedNothing && !leaving.current) discardEmpty();
      setError(chatErrorMessage(failure));
      // Give the question and its images back so they can be sent again (unless the user already typed or attached more).
      setDraft((current) => current || text);
      setAttachments((current) => (current.length > 0 ? current : sentImages));
    } finally {
      if (request.current?.controller === controller) {
        request.current = null;
        setPendingQuestion(null);
        setPendingImages([]);
        setPendingAnswer("");
      }
    }
  }

  /** Stops the answer: what arrived stays in the conversation, marked as stopped; with nothing yet, the question comes back. */
  function stop() {
    const current = request.current;
    if (!current) return;
    request.current = null;
    current.controller.abort();
    const partial = answerSoFar.current;
    if (partial.trim()) {
      // The server saves the question with this text when the request closes (marked as stopped).
      if (conversationId.current) onStopped?.(conversationId.current, current.question);
      const id = nextId.current;
      nextId.current += 2;
      setMessages((existing) => [
        ...existing,
        {
          id: `m${id}`,
          role: "user",
          text: current.question,
          images: current.images.length > 0 ? current.images.map(({ name, previewUrl }) => ({ name, src: previewUrl })) : undefined,
        },
        { id: `m${id + 1}`, role: "assistant", text: partial, stopped: true },
      ]);
    } else {
      // Stopped before any text: the server saved nothing.
      if (current.created) discardEmpty();
      setDraft((draftNow) => draftNow || current.text);
      setAttachments((attachedNow) => (attachedNow.length > 0 ? attachedNow : current.images));
    }
    setPendingQuestion(null);
    setPendingImages([]);
    setPendingAnswer("");
  }

  /** Deletes the conversation created for a question that saved nothing, so the list keeps no empty chats. */
  function discardEmpty() {
    const id = conversationId.current;
    if (!id) return;
    conversationId.current = null;
    void deleteConversation(id).catch(() => undefined);
    onDiscarded?.(id);
  }

  function newConversation() {
    if (onNewConversation) {
      const current = request.current;
      request.current = null;
      current?.controller.abort();
      if (current?.created && !answerSoFar.current.trim()) discardEmpty();
      else if (current && answerSoFar.current.trim() && conversationId.current) onStopped?.(conversationId.current, current.question);
      onNewConversation();
      return;
    }
    conversationId.current = null;
    request.current?.controller.abort();
    request.current = null;
    setPendingQuestion(null);
    setPendingImages([]);
    setPendingAnswer("");
    setMessages([]);
    setError(null);
    setInputError(null);
    setAttachments([]);
  }

  async function addFiles(files: File[]) {
    const free = MAX_IMAGES_PER_QUESTION - attachments.length;
    let problem: LocalizedError | null = files.length > free ? { key: "image.tooMany", params: { max: MAX_IMAGES_PER_QUESTION } } : null;
    const added: ImageAttachment[] = [];
    for (const file of files.slice(0, Math.max(free, 0))) {
      const result = await readImageFile(file);
      if (result.ok) {
        added.push({ ...result.attachment, id: `a${nextId.current++}` });
      } else {
        problem ??= { key: `image.${result.reason}`, params: { name: result.name, max: MAX_IMAGE_MEGABYTES } };
      }
    }
    setAttachments((current) => [...current, ...added].slice(0, MAX_IMAGES_PER_QUESTION));
    setInputError(problem);
  }

  const isEmpty = messages.length === 0 && !pendingQuestion;
  const usage = conversationUsage(messages);

  return (
    <div className={styles.root}>
      <div className={styles.top}>
        <PageHeader
          title={t("chat.title")}
          subtitle={t("chat.subtitle")}
          actions={
            <div className={styles.actions}>
              <ModelPicker list={modelList} value={model} onChange={chooseModel} />
              <Button icon={<ArrowCounterclockwise20Regular />} onClick={newConversation} disabled={isEmpty}>
                {t("chat.new")}
              </Button>
            </div>
          }
        />
      </div>

      <div className={styles.scroller}>
        <div className={styles.conversation}>
          {opening ? (
            <Spinner size="small" label={t("saved.opening")} />
          ) : isEmpty ? (
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
            <ChatMessageList messages={messages} pendingQuestion={pendingQuestion} pendingImages={pendingImages} pendingAnswer={pendingAnswer} />
          )}
        </div>
      </div>

      <div className={styles.bottom}>
        <div className={styles.composer}>
          {usage && (
            <div className={styles.usage} aria-live="polite">
              <Caption1>
                {t("chat.usage.total", { total: usage.totalTokens, input: usage.inputTokens, output: usage.outputTokens, answers: usage.answers })}
              </Caption1>
              {usage.cost != null && <Caption1>{t("chat.usage.cost", { cost: formatCost(usage.cost, locale) })}</Caption1>}
              <Caption1 className={styles.usageHint}>{t("chat.usage.hint")}</Caption1>
            </div>
          )}
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
            disabled={full || opening}
            validationMessage={inputError ? t(inputError.key, inputError.params) : undefined}
            attachments={attachments}
            onAddFiles={(files) => void addFiles(files)}
            onRemoveAttachment={(id) => setAttachments((current) => current.filter((attachment) => attachment.id !== id))}
            onStop={stop}
          />
        </div>
      </div>
    </div>
  );
}
