"use client";

import { Button, Caption1, Field, makeStyles, Textarea, tokens, Tooltip } from "@fluentui/react-components";
import { Attach20Regular, Dismiss12Regular, Send20Regular } from "@fluentui/react-icons";
import { useRef, type ClipboardEvent, type FormEvent, type KeyboardEvent } from "react";
import { MAX_QUESTION_LENGTH } from "@/lib/chat/chatRules";
import { MAX_IMAGES_PER_QUESTION, SUPPORTED_IMAGE_TYPES, type ImageAttachment } from "@/lib/chat/images";
import { useI18n } from "@/lib/i18n/I18nProvider";

const useStyles = makeStyles({
  form: { display: "flex", flexDirection: "column", gap: tokens.spacingVerticalS },
  row: { display: "flex", gap: tokens.spacingHorizontalS, alignItems: "flex-end" },
  field: { flexGrow: 1 },
  textarea: { width: "100%" },
  hiddenInput: { display: "none" },
  attachments: { display: "flex", flexWrap: "wrap", gap: tokens.spacingHorizontalS, margin: 0, padding: 0, listStyle: "none" },
  attachment: {
    position: "relative",
    width: "64px",
    height: "64px",
    borderRadius: tokens.borderRadiusMedium,
    overflow: "hidden",
    border: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralStroke1}`,
    backgroundColor: tokens.colorNeutralBackground1,
  },
  thumbnail: { width: "100%", height: "100%", objectFit: "cover", display: "block" },
  remove: { position: "absolute", top: "2px", right: "2px", minWidth: "auto" },
  footer: { display: "flex", justifyContent: "flex-end", color: tokens.colorNeutralForeground3 },
});

/** The counter appears once the question gets close to the limit. */
const COUNTER_FROM = Math.floor(MAX_QUESTION_LENGTH * 0.8);

interface ChatComposerProps {
  value: string;
  onChange: (value: string) => void;
  onSubmit: () => void;
  /** A question is being answered: typing and attaching are allowed, sending is not. */
  busy: boolean;
  /** The conversation cannot take more questions. */
  disabled: boolean;
  validationMessage?: string;
  attachments: readonly ImageAttachment[];
  onAddFiles: (files: File[]) => void;
  onRemoveAttachment: (id: string) => void;
}

export function ChatComposer({
  value,
  onChange,
  onSubmit,
  busy,
  disabled,
  validationMessage,
  attachments,
  onAddFiles,
  onRemoveAttachment,
}: ChatComposerProps) {
  const styles = useStyles();
  const { t } = useI18n();
  const fileInput = useRef<HTMLInputElement>(null);
  const full = attachments.length >= MAX_IMAGES_PER_QUESTION;

  function submit(event: FormEvent) {
    event.preventDefault();
    if (!busy && !disabled) onSubmit();
  }

  function onKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    // Enter sends; Shift+Enter adds a line; Enter while composing text with an IME (e.g. accents) does neither.
    if (event.key === "Enter" && !event.shiftKey && !event.nativeEvent.isComposing) {
      event.preventDefault();
      if (!busy && !disabled) onSubmit();
    }
  }

  // Pasted images (e.g. a screenshot) are attached; pasted text goes into the question as usual.
  function onPaste(event: ClipboardEvent<HTMLTextAreaElement>) {
    const files = Array.from(event.clipboardData.files).filter((file) => file.type.startsWith("image/"));
    if (files.length === 0) return;
    event.preventDefault();
    onAddFiles(files);
  }

  return (
    <form className={styles.form} onSubmit={submit}>
      {attachments.length > 0 && (
        <ul className={styles.attachments} aria-label={t("chat.attachments")}>
          {attachments.map((attachment) => (
            <li key={attachment.id} className={styles.attachment}>
              {/* eslint-disable-next-line @next/next/no-img-element -- local data: URI preview, nothing to optimise */}
              <img src={attachment.previewUrl} alt={t("chat.image.alt", { name: attachment.name })} className={styles.thumbnail} />
              <Button
                className={styles.remove}
                size="small"
                shape="circular"
                icon={<Dismiss12Regular />}
                aria-label={t("chat.attachment.remove", { name: attachment.name })}
                onClick={() => onRemoveAttachment(attachment.id)}
              />
            </li>
          ))}
        </ul>
      )}
      <div className={styles.row}>
        <input
          ref={fileInput}
          type="file"
          accept={SUPPORTED_IMAGE_TYPES.join(",")}
          multiple
          className={styles.hiddenInput}
          data-testid="image-input"
          tabIndex={-1}
          aria-hidden
          onChange={(event) => {
            onAddFiles(Array.from(event.target.files ?? []));
            // Picking the same file again must fire onChange again.
            event.target.value = "";
          }}
        />
        <Tooltip content={t("chat.attach")} relationship="label">
          <Button icon={<Attach20Regular />} disabled={disabled || full} onClick={() => fileInput.current?.click()} />
        </Tooltip>
        <Field
          className={styles.field}
          label={t("chat.input.label")}
          validationMessage={validationMessage}
          validationState={validationMessage ? "error" : "none"}
        >
          <Textarea
            className={styles.textarea}
            value={value}
            onChange={(_, data) => onChange(data.value)}
            onKeyDown={onKeyDown}
            onPaste={onPaste}
            placeholder={t("chat.input.placeholder")}
            resize="vertical"
            rows={2}
            disabled={disabled}
          />
        </Field>
        <Button type="submit" appearance="primary" icon={<Send20Regular />} disabled={busy || disabled || value.trim().length === 0}>
          {t("chat.send")}
        </Button>
      </div>
      {value.length >= COUNTER_FROM && (
        <Caption1 className={styles.footer} aria-live="polite">
          {t("chat.input.counter", { count: value.length, max: MAX_QUESTION_LENGTH })}
        </Caption1>
      )}
    </form>
  );
}
