"use client";

import { Button, Caption1, Field, makeStyles, Textarea, tokens } from "@fluentui/react-components";
import { Send20Regular } from "@fluentui/react-icons";
import type { FormEvent, KeyboardEvent } from "react";
import { MAX_QUESTION_LENGTH } from "@/lib/chat/chatRules";
import { useI18n } from "@/lib/i18n/I18nProvider";

const useStyles = makeStyles({
  form: { display: "flex", gap: tokens.spacingHorizontalM, alignItems: "flex-end" },
  field: { flexGrow: 1 },
  textarea: { width: "100%" },
  footer: { display: "flex", justifyContent: "flex-end", color: tokens.colorNeutralForeground3 },
});

/** The counter appears once the question gets close to the limit. */
const COUNTER_FROM = Math.floor(MAX_QUESTION_LENGTH * 0.8);

interface ChatComposerProps {
  value: string;
  onChange: (value: string) => void;
  onSubmit: () => void;
  /** A question is being answered: typing is allowed, sending is not. */
  busy: boolean;
  /** The conversation cannot take more questions. */
  disabled: boolean;
  validationMessage?: string;
}

export function ChatComposer({ value, onChange, onSubmit, busy, disabled, validationMessage }: ChatComposerProps) {
  const styles = useStyles();
  const { t } = useI18n();

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

  return (
    <form className={styles.form} onSubmit={submit}>
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
          placeholder={t("chat.input.placeholder")}
          resize="vertical"
          rows={2}
          disabled={disabled}
        />
      </Field>
      <Button type="submit" appearance="primary" icon={<Send20Regular />} disabled={busy || disabled || value.trim().length === 0}>
        {t("chat.send")}
      </Button>
      {value.length >= COUNTER_FROM && (
        <Caption1 className={styles.footer} aria-live="polite">
          {t("chat.input.counter", { count: value.length, max: MAX_QUESTION_LENGTH })}
        </Caption1>
      )}
    </form>
  );
}
