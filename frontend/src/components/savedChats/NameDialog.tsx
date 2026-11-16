"use client";

import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Field,
  Input,
} from "@fluentui/react-components";
import { useState, type FormEvent } from "react";
import type { LocalizedError } from "@/lib/chat/chatErrors";
import { checkName } from "@/lib/chat/savedChats";
import { useI18n } from "@/lib/i18n/I18nProvider";

interface NameDialogProps {
  open: boolean;
  title: string;
  label: string;
  submitLabel: string;
  initialValue?: string;
  max: number;
  /** Saves the name; resolves with the error to show under the field, or null when done. */
  onSubmit: (value: string) => Promise<LocalizedError | null>;
  onClose: () => void;
}

/** A modal dialog with one name field (new folder, rename a folder or a chat). Remounted (with a new key) for every opening, so it starts fresh; closing keeps it mounted until Fluent has closed it. */
export function NameDialog({ open, title, label, submitLabel, initialValue = "", max, onSubmit, onClose }: NameDialogProps) {
  const { t } = useI18n();
  const [value, setValue] = useState(initialValue);
  const [error, setError] = useState<LocalizedError | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (busy) return;
    const check = checkName(value, max);
    if (!check.ok) {
      setError({ key: `saved.name.${check.reason}`, params: { max } });
      return;
    }
    setBusy(true);
    const problem = await onSubmit(check.value);
    setBusy(false);
    if (problem) setError(problem);
    else onClose();
  }

  return (
    <Dialog open={open} modalType="modal" onOpenChange={(_, data) => !data.open && onClose()}>
      <DialogSurface>
        <form onSubmit={submit} noValidate>
          <DialogBody>
            <DialogTitle>{title}</DialogTitle>
            <DialogContent>
              <Field label={label} validationMessage={error ? t(error.key, error.params) : undefined} validationState={error ? "error" : "none"}>
                <Input
                  value={value}
                  
                  onChange={(_, data) => {
                    setValue(data.value);
                    if (error) setError(null);
                  }}
                />
              </Field>
            </DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={onClose}>
                {t("saved.cancel")}
              </Button>
              <Button type="submit" appearance="primary" disabled={busy}>
                {submitLabel}
              </Button>
            </DialogActions>
          </DialogBody>
        </form>
      </DialogSurface>
    </Dialog>
  );
}
