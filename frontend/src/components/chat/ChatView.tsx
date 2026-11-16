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
import { ModelPicker, type ModelList } from "@/components/chat/ModelPicker";
import { PageBody, PageHeader } from "@/components/ui/PageLayout";
import { chatErrorMessage, type LocalizedError } from "@/lib/chat/chatErrors";
import { checkQuestion, MAX_CONVERSATION_QUESTIONS, MAX_QUESTION_LENGTH } from "@/lib/chat/chatRules";
import { MAX_IMAGE_MEGABYTES, MAX_IMAGES_PER_QUESTION, readImageFile, type ImageAttachment } from "@/lib/chat/images";
import { questionCount, toHistory, type ChatMessage } from "@/lib/chat/conversation";
import { initialModel, readModelPreference, storeModelPreference } from "@/lib/chat/modelPreference";
import { useI18n } from "@/lib/i18n/I18nProvider";
import type { MessageKey } from "@/lib/i18n/messages/en";
import { streamChat } from "@/services/chatService";
import { getModels } from "@/services/modelsService";

const SUGGESTIONS: MessageKey[] = ["chat.suggestion.1", "chat.suggestion.2", "chat.suggestion.3"];

const useStyles = makeStyles({
  page: { gap: tokens.spacingVerticalL },
  actions: { display: "flex", alignItems: "center", flexWrap: "wrap", gap: tokens.spacingHorizontalM },
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
 * Backlog step 05: the chat in the browser; step 07 streams the answer (POST /api/chat/stream) with a Stop button. The conversation lives in this component's state (one tab, lost on
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
  const [attachments, setAttachments] = useState<ImageAttachment[]>([]);
  const [pendingImages, setPendingImages] = useState<{ name: string; src: string }[]>([]);
  // The answer so far, in a ref for Stop (state updates are batched) and in state for rendering.
  const [pendingAnswer, setPendingAnswer] = useState("");
  const answerSoFar = useRef("");
  const [modelList, setModelList] = useState<ModelList>({ state: "loading" });
  const [model, setModel] = useState<string | null>(null);
  const request = useRef<{ controller: AbortController; question: string; text: string; images: ImageAttachment[] } | null>(null);
  const nextId = useRef(0);

  // Leaving the page cancels the question in flight.
  useEffect(() => () => request.current?.controller.abort(), []);

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
    const sentImages = attachments;
    setAttachments([]);
    setPendingImages(sentImages.map(({ name, previewUrl }) => ({ name, src: previewUrl })));
    answerSoFar.current = "";
    setPendingAnswer("");
    const controller = new AbortController();
    request.current = { controller, question: check.question, text, images: sentImages };

    try {
      const response = await streamChat(
        {
          question: check.question,
          history: toHistory(messages),
          ...(sentImages.length > 0 && { images: sentImages.map(({ mediaType, data }) => ({ mediaType, data })) }),
          ...(requestedModel && { model: requestedModel }),
        },
        (piece) => {
          answerSoFar.current += piece;
          setPendingAnswer(answerSoFar.current);
        },
        controller.signal,
      );
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
        { id: `m${id + 1}`, role: "assistant", text: response.answer, model: response.model, truncated: response.truncated },
      ]);
    } catch (failure) {
      if (controller.signal.aborted) return;
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
      setDraft((draftNow) => draftNow || current.text);
      setAttachments((attachedNow) => (attachedNow.length > 0 ? attachedNow : current.images));
    }
    setPendingQuestion(null);
    setPendingImages([]);
    setPendingAnswer("");
  }

  function newConversation() {
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

  return (
    <PageBody className={styles.page}>
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
          <ChatMessageList messages={messages} pendingQuestion={pendingQuestion} pendingImages={pendingImages} pendingAnswer={pendingAnswer} />
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
          attachments={attachments}
          onAddFiles={(files) => void addFiles(files)}
          onRemoveAttachment={(id) => setAttachments((current) => current.filter((attachment) => attachment.id !== id))}
          onStop={stop}
        />
      </div>
    </PageBody>
  );
}
