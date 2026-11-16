"use client";

import { Caption1, Dropdown, makeStyles, Option, tokens } from "@fluentui/react-components";
import { useId } from "react";
import { useI18n } from "@/lib/i18n/I18nProvider";
import type { ChatModel } from "@/services/types";

const useStyles = makeStyles({
  picker: { display: "flex", alignItems: "center", gap: tokens.spacingHorizontalS },
  dropdown: { minWidth: "220px" },
  hint: { color: tokens.colorNeutralForeground3, maxWidth: "220px" },
});

export type ModelList = { state: "loading" } | { state: "failed" } | { state: "ready"; models: ChatModel[]; defaultModel: string };

interface ModelPickerProps {
  list: ModelList;
  value: string | null;
  onChange: (model: string) => void;
}

/** Chat model for the next questions (step 08); the conversation goes on when it changes. */
export function ModelPicker({ list, value, onChange }: ModelPickerProps) {
  const styles = useStyles();
  const { t } = useI18n();
  const labelId = useId();
  const hintId = useId();

  const label = (model: ChatModel) => (model.isDefault ? t("chat.model.default", { model: model.id }) : model.id);
  // Same element in every state (no wrapper appears on failure), so it is never re-mounted under the user.
  const dropdown = (
    <Dropdown
      className={styles.dropdown}
      aria-labelledby={labelId}
      aria-describedby={list.state === "failed" ? hintId : undefined}
      disabled={list.state !== "ready"}
      value={
        list.state === "loading"
          ? t("chat.model.loading")
          : list.state === "failed"
            ? t("chat.model.unavailable")
            : label(list.models.find((model) => model.id === value) ?? list.models[0])
      }
      selectedOptions={value ? [value] : []}
      onOptionSelect={(_, data) => data.optionValue && onChange(data.optionValue)}
    >
      {list.state === "ready" &&
        list.models.map((model) => (
          <Option key={model.id} value={model.id} text={label(model)}>
            {label(model)}
          </Option>
        ))}
    </Dropdown>
  );

  return (
    <div className={styles.picker}>
      <span id={labelId}>{t("chat.model.label")}</span>
      {dropdown}
      {list.state === "failed" && (
        <Caption1 id={hintId} className={styles.hint}>
          {t("chat.model.unavailableHint")}
        </Caption1>
      )}
    </div>
  );
}
