"use client";

import {
  Button,
  ColorSwatch,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Field,
  Label,
  makeStyles,
  SwatchPicker,
  Text,
  tokens,
} from "@fluentui/react-components";
import { useId, useState, type FormEvent } from "react";
import type { LocalizedError } from "@/lib/chat/chatErrors";
import { FOLDER_COLOR_TOKENS, FolderGlyph } from "@/lib/chat/folderAppearance";
import { useI18n } from "@/lib/i18n/I18nProvider";
import { FOLDER_COLORS, FOLDER_ICONS, type Folder, type FolderColor, type FolderIcon } from "@/services/types";

const useStyles = makeStyles({
  content: { display: "flex", flexDirection: "column", gap: tokens.spacingVerticalL },
  preview: {
    display: "flex",
    alignItems: "center",
    gap: tokens.spacingHorizontalS,
    padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalM}`,
    borderRadius: tokens.borderRadiusMedium,
    backgroundColor: tokens.colorNeutralBackground2,
  },
  group: { display: "flex", flexDirection: "column", gap: tokens.spacingVerticalXS },
  swatches: { flexWrap: "wrap" },
});

interface FolderAppearanceDialogProps {
  open: boolean;
  folder: Folder;
  onSave: (icon: FolderIcon, color: FolderColor) => Promise<LocalizedError | null>;
  onClose: () => void;
}

/** Chooses a folder's icon and colour, with a preview. Remounted (with a new key) for every opening. */
export function FolderAppearanceDialog({ open, folder, onSave, onClose }: FolderAppearanceDialogProps) {
  const styles = useStyles();
  const { t } = useI18n();
  const iconLabel = useId();
  const colorLabel = useId();
  const [icon, setIcon] = useState<FolderIcon>(folder.icon);
  const [color, setColor] = useState<FolderColor>(folder.color);
  const [error, setError] = useState<LocalizedError | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (icon === folder.icon && color === folder.color) {
      onClose();
      return;
    }
    setBusy(true);
    const problem = await onSave(icon, color);
    setBusy(false);
    if (problem) setError(problem);
    else onClose();
  }

  return (
    <Dialog open={open} modalType="modal" onOpenChange={(_, data) => !data.open && onClose()}>
      <DialogSurface>
        <form onSubmit={submit}>
          <DialogBody>
            <DialogTitle>{t("saved.appearance.title")}</DialogTitle>
            <DialogContent className={styles.content}>
              <div className={styles.preview} aria-hidden>
                <FolderGlyph icon={icon} color={color} />
                <Text weight="semibold">{folder.name}</Text>
              </div>
              <div className={styles.group}>
                <Label id={iconLabel}>{t("saved.appearance.icon")}</Label>
                <SwatchPicker
                  aria-labelledby={iconLabel}
                  layout="row"
                  className={styles.swatches}
                  selectedValue={icon}
                  onSelectionChange={(_, data) => setIcon(data.selectedValue as FolderIcon)}
                >
                  {FOLDER_ICONS.map((key) => (
                    <ColorSwatch
                      key={key}
                      value={key}
                      color={tokens.colorNeutralBackground1}
                      borderColor={tokens.colorNeutralStroke1}
                      icon={<FolderGlyph icon={key} color={color} />}
                      aria-label={t(`folder.icon.${key}`)}
                    />
                  ))}
                </SwatchPicker>
              </div>
              <div className={styles.group}>
                <Label id={colorLabel}>{t("saved.appearance.color")}</Label>
                <SwatchPicker
                  aria-labelledby={colorLabel}
                  layout="row"
                  className={styles.swatches}
                  selectedValue={color}
                  onSelectionChange={(_, data) => setColor(data.selectedValue as FolderColor)}
                >
                  {FOLDER_COLORS.map((key) => (
                    <ColorSwatch key={key} value={key} color={FOLDER_COLOR_TOKENS[key]} aria-label={t(`folder.color.${key}`)} />
                  ))}
                </SwatchPicker>
              </div>
              {error && <Field validationMessage={t(error.key, error.params)} validationState="error" />}
            </DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={onClose}>
                {t("saved.cancel")}
              </Button>
              <Button type="submit" appearance="primary" disabled={busy}>
                {t("saved.save")}
              </Button>
            </DialogActions>
          </DialogBody>
        </form>
      </DialogSurface>
    </Dialog>
  );
}
