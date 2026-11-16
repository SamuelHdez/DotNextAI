"use client";

import { Button, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle, Field } from "@fluentui/react-components";
import { useState } from "react";
import type { LocalizedError } from "@/lib/chat/chatErrors";
import { useI18n } from "@/lib/i18n/I18nProvider";

interface ConfirmDialogProps {
  open: boolean;
  title: string;
  body: string;
  confirmLabel: string;
  /** Does the action; resolves with the error to show, or null when done. */
  onConfirm: () => Promise<LocalizedError | null>;
  onClose: () => void;
}

/** An alert dialog that asks before deleting something. Remounted (with a new key) for every opening, so it starts fresh; closing keeps it mounted until Fluent has closed it. */
export function ConfirmDialog({ open, title, body, confirmLabel, onConfirm, onClose }: ConfirmDialogProps) {
  const { t } = useI18n();
  const [error, setError] = useState<LocalizedError | null>(null);
  const [busy, setBusy] = useState(false);

  async function confirm() {
    setBusy(true);
    const problem = await onConfirm();
    setBusy(false);
    if (problem) setError(problem);
    else onClose();
  }

  return (
    <Dialog open={open} modalType="alert" onOpenChange={(_, data) => !data.open && onClose()}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>{title}</DialogTitle>
          <DialogContent>
            {body}
            {error && <Field validationMessage={t(error.key, error.params)} validationState="error" />}
          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={onClose}>
              {t("saved.cancel")}
            </Button>
            <Button appearance="primary" onClick={() => void confirm()} disabled={busy}>
              {confirmLabel}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
