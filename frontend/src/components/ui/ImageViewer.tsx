"use client";

import { Button, Dialog, DialogBody, DialogContent, DialogSurface, DialogTitle, makeStyles, tokens } from "@fluentui/react-components";
import { Dismiss24Regular } from "@fluentui/react-icons";
import { useState } from "react";
import { useI18n } from "@/lib/i18n/I18nProvider";

export interface ViewedImage {
  src: string;
  /** Text alternative; also the dialog's title. */
  alt: string;
}

const useStyles = makeStyles({
  surface: { width: "auto", maxWidth: "min(92vw, 1400px)" },
  content: { display: "flex", justifyContent: "center" },
  image: {
    display: "block",
    maxWidth: "100%",
    maxHeight: "78vh",
    objectFit: "contain",
    borderRadius: tokens.borderRadiusMedium,
    backgroundColor: tokens.colorNeutralBackground3,
  },
});

/**
 * An image at full size in a modal dialog (Escape, the close button or a click outside close it). Stays mounted while
 * it closes, keeping the last image, so Fluent restores the page's accessibility tree.
 */
export function ImageViewer({ image, onClose }: { image: ViewedImage | null; onClose: () => void }) {
  const styles = useStyles();
  const { t } = useI18n();
  const [shown, setShown] = useState<ViewedImage | null>(image);
  // Keep the last image during the closing animation (state adjusted while rendering, not in an effect).
  if (image && image !== shown) setShown(image);

  return (
    <Dialog open={image !== null} modalType="modal" onOpenChange={(_, data) => !data.open && onClose()}>
      <DialogSurface className={styles.surface}>
        <DialogBody>
          <DialogTitle
            action={<Button appearance="subtle" aria-label={t("image.viewer.close")} icon={<Dismiss24Regular />} onClick={onClose} />}
          >
            {shown?.alt}
          </DialogTitle>
          <DialogContent className={styles.content}>
            {/* eslint-disable-next-line @next/next/no-img-element -- data: URI or an API image, nothing to optimise */}
            {shown && <img src={shown.src} alt={shown.alt} className={styles.image} />}
          </DialogContent>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
