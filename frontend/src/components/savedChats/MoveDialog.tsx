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
  Select,
} from "@fluentui/react-components";
import { useState, type FormEvent } from "react";
import type { LocalizedError } from "@/lib/chat/chatErrors";
import { useI18n } from "@/lib/i18n/I18nProvider";
import type { Folder } from "@/services/types";

const NO_FOLDER = "";

interface MoveDialogProps {
  open: boolean;
  chatTitle: string;
  folders: readonly Folder[];
  currentFolderId: string | null;
  onMove: (folderId: string | null) => Promise<LocalizedError | null>;
  onClose: () => void;
}

/** Moves a chat into a folder or out of every folder (one level). Remounted (with a new key) for every opening, so it starts fresh; closing keeps it mounted until Fluent has closed it. */
export function MoveDialog({ open, chatTitle, folders, currentFolderId, onMove, onClose }: MoveDialogProps) {
  const { t } = useI18n();
  const [value, setValue] = useState(currentFolderId ?? NO_FOLDER);
  const [error, setError] = useState<LocalizedError | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if ((currentFolderId ?? NO_FOLDER) === value) {
      onClose();
      return;
    }
    setBusy(true);
    const problem = await onMove(value === NO_FOLDER ? null : value);
    setBusy(false);
    if (problem) setError(problem);
    else onClose();
  }

  return (
    <Dialog open={open} modalType="modal" onOpenChange={(_, data) => !data.open && onClose()}>
      <DialogSurface>
        <form onSubmit={submit}>
          <DialogBody>
            <DialogTitle>{t("saved.moveTitle")}</DialogTitle>
            <DialogContent>
              <Field
                label={t("saved.moveLabel", { name: chatTitle })}
                validationMessage={error ? t(error.key, error.params) : undefined}
                validationState={error ? "error" : "none"}
              >
                <Select value={value} onChange={(_, data) => setValue(data.value)}>
                  <option value={NO_FOLDER}>{t("saved.moveNone")}</option>
                  {folders.map((folder) => (
                    <option key={folder.id} value={folder.id}>
                      {folder.name}
                    </option>
                  ))}
                </Select>
              </Field>
            </DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={onClose}>
                {t("saved.cancel")}
              </Button>
              <Button type="submit" appearance="primary" disabled={busy}>
                {t("saved.moveConfirm")}
              </Button>
            </DialogActions>
          </DialogBody>
        </form>
      </DialogSurface>
    </Dialog>
  );
}
