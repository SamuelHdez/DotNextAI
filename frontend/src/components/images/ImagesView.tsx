"use client";

import {
  Body1,
  Button,
  Caption1,
  Card,
  CardFooter,
  CardPreview,
  Field,
  makeStyles,
  MessageBar,
  MessageBarActions,
  MessageBarBody,
  Radio,
  RadioGroup,
  Spinner,
  Textarea,
  tokens,
} from "@fluentui/react-components";
import { ArrowDownload20Regular, Dismiss20Regular, ImageSparkle20Regular, Sparkle20Regular } from "@fluentui/react-icons";
import { useEffect, useRef, useState, type FormEvent } from "react";
import { PageBody, PageHeader } from "@/components/ui/PageLayout";
import { chatErrorMessage, type LocalizedError } from "@/lib/chat/chatErrors";
import { useI18n } from "@/lib/i18n/I18nProvider";
import { checkPrompt, downloadName, MAX_PROMPT_LENGTH } from "@/lib/images/imageRules";
import { generateImage } from "@/services/imagesService";
import type { ImageSize } from "@/services/types";

const SIZES: ImageSize[] = ["square", "landscape", "portrait"];

const useStyles = makeStyles({
  form: { display: "flex", flexDirection: "column", gap: tokens.spacingVerticalM },
  actions: { display: "flex", alignItems: "center", flexWrap: "wrap", gap: tokens.spacingHorizontalL },
  gallery: {
    display: "grid",
    gridTemplateColumns: "repeat(auto-fill, minmax(280px, 1fr))",
    gap: tokens.spacingHorizontalL,
    margin: 0,
    padding: 0,
    listStyle: "none",
  },
  image: { width: "100%", height: "auto", display: "block", backgroundColor: tokens.colorNeutralBackground3 },
  caption: { color: tokens.colorNeutralForeground3, overflowWrap: "anywhere" },
  empty: {
    display: "flex",
    flexDirection: "column",
    alignItems: "center",
    gap: tokens.spacingVerticalS,
    paddingBlock: tokens.spacingVerticalXXL,
    color: tokens.colorNeutralForeground3,
  },
  emptyIcon: { fontSize: "32px", color: tokens.colorBrandForeground1 },
});

interface Generated {
  id: number;
  prompt: string;
  src: string;
  mediaType: string;
  model: string;
  revisedPrompt: string | null;
}

/**
 * Backlog step 09: image generation. Images live in this page's state (newest first) until the user leaves it; each
 * one can be downloaded. The description is sent to POST /api/images, which uses the server's image model.
 */
export function ImagesView() {
  const styles = useStyles();
  const { t } = useI18n();
  const [prompt, setPrompt] = useState("");
  const [size, setSize] = useState<ImageSize>("square");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<LocalizedError | null>(null);
  const [inputError, setInputError] = useState<LocalizedError | null>(null);
  const [images, setImages] = useState<Generated[]>([]);
  const request = useRef<AbortController | null>(null);
  const nextId = useRef(1);

  // Leaving the page cancels a generation in flight.
  useEffect(() => () => request.current?.abort(), []);

  async function generate(event: FormEvent) {
    event.preventDefault();
    if (busy) return;
    const check = checkPrompt(prompt);
    if (!check.ok) {
      setInputError(
        check.reason === "empty"
          ? { key: "images.prompt.empty" }
          : { key: "images.prompt.tooLong", params: { count: check.length, max: MAX_PROMPT_LENGTH } },
      );
      return;
    }

    setInputError(null);
    setError(null);
    setBusy(true);
    const controller = new AbortController();
    request.current = controller;
    try {
      const image = await generateImage({ prompt: check.prompt, size }, controller.signal);
      const id = nextId.current++;
      setImages((current) => [
        {
          id,
          prompt: check.prompt,
          src: `data:${image.mediaType};base64,${image.data}`,
          mediaType: image.mediaType,
          model: image.model,
          revisedPrompt: image.revisedPrompt,
        },
        ...current,
      ]);
    } catch (failure) {
      if (!controller.signal.aborted) setError(chatErrorMessage(failure));
    } finally {
      if (request.current === controller) {
        request.current = null;
        setBusy(false);
      }
    }
  }

  return (
    <PageBody>
      <PageHeader title={t("images.title")} subtitle={t("images.subtitle")} />

      <form className={styles.form} onSubmit={generate}>
        <Field
          label={t("images.prompt.label")}
          validationMessage={inputError ? t(inputError.key, inputError.params) : undefined}
          validationState={inputError ? "error" : "none"}
        >
          <Textarea
            value={prompt}
            onChange={(_, data) => {
              setPrompt(data.value);
              if (inputError) setInputError(null);
            }}
            placeholder={t("images.prompt.placeholder")}
            resize="vertical"
            rows={3}
          />
        </Field>
        <div className={styles.actions}>
          <Field label={t("images.size.label")}>
            <RadioGroup layout="horizontal" value={size} onChange={(_, data) => setSize(data.value as ImageSize)}>
              {SIZES.map((option) => (
                <Radio key={option} value={option} label={t(`images.size.${option}`)} />
              ))}
            </RadioGroup>
          </Field>
          <Button type="submit" appearance="primary" icon={<Sparkle20Regular />} disabled={busy}>
            {t("images.generate")}
          </Button>
          {busy && <Spinner size="tiny" label={t("images.generating")} />}
        </div>
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
      </form>

      {images.length === 0 ? (
        <div className={styles.empty}>
          <ImageSparkle20Regular className={styles.emptyIcon} aria-hidden />
          <Body1>{t("images.empty")}</Body1>
        </div>
      ) : (
        <ul className={styles.gallery} aria-label={t("images.title")}>
          {images.map((image) => (
            <li key={image.id}>
              <Card>
                <CardPreview>
                  {/* eslint-disable-next-line @next/next/no-img-element -- data: URI of a generated image, nothing to optimise */}
                  <img src={image.src} alt={t("images.alt", { prompt: image.prompt })} className={styles.image} />
                </CardPreview>
                <Caption1 className={styles.caption}>
                  {image.revisedPrompt ? t("images.revised", { prompt: image.revisedPrompt }) : image.prompt}
                </Caption1>
                <CardFooter>
                  <Caption1 className={styles.caption}>{image.model}</Caption1>
                  <Button as="a" href={image.src} download={downloadName(image.id, image.mediaType)} icon={<ArrowDownload20Regular />}>
                    {t("images.download")}
                  </Button>
                </CardFooter>
              </Card>
            </li>
          ))}
        </ul>
      )}
    </PageBody>
  );
}
