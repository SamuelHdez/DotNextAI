"use client";

import { Badge, Body1, Button, Caption1, Card, CardHeader, makeStyles, Spinner, Subtitle1, tokens } from "@fluentui/react-components";
import { ArrowClockwise20Regular } from "@fluentui/react-icons";
import { useCallback, useEffect, useState } from "react";
import { useI18n } from "@/lib/i18n/I18nProvider";
import { getHealth } from "@/services/healthService";
import type { HealthStatus } from "@/services/types";

const useStyles = makeStyles({
  rows: { display: "flex", flexDirection: "column", gap: tokens.spacingVerticalM },
  row: { display: "flex", alignItems: "center", justifyContent: "space-between", gap: tokens.spacingHorizontalM },
  hint: { color: tokens.colorNeutralForeground3 },
});

type State = { kind: "checking" } | { kind: "online"; health: HealthStatus } | { kind: "offline" };

/** Whether the API answers and whether the LiteLLM proxy is configured, from GET /api/health. */
export function ServiceStatusCard() {
  const styles = useStyles();
  const { t } = useI18n();
  const [state, setState] = useState<State>({ kind: "checking" });

  // Only async callbacks update the state, so the effect never sets it synchronously.
  const load = useCallback((signal?: AbortSignal) => {
    getHealth(signal)
      .then((health) => setState({ kind: "online", health }))
      .catch((error: unknown) => {
        if (error instanceof DOMException && error.name === "AbortError") return;
        setState({ kind: "offline" });
      });
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    load(controller.signal);
    return () => controller.abort();
  }, [load]);

  function refresh() {
    setState({ kind: "checking" });
    load();
  }

  const configured = state.kind === "online" ? state.health.llmConfigured : undefined;

  return (
    <Card>
      <CardHeader
        header={<Subtitle1 as="h2">{t("home.status.title")}</Subtitle1>}
        action={
          <Button
            appearance="subtle"
            icon={<ArrowClockwise20Regular />}
            aria-label={t("home.status.refresh")}
            title={t("home.status.refresh")}
            disabled={state.kind === "checking"}
            onClick={refresh}
          />
        }
      />
      <div className={styles.rows} aria-live="polite">
        <div className={styles.row}>
          <Body1>{t("home.status.api")}</Body1>
          {state.kind === "checking" ? (
            <Spinner size="extra-tiny" label={t("home.status.checking")} labelPosition="before" />
          ) : state.kind === "online" ? (
            <Badge appearance="tint" color="success">
              {t("home.status.online")}
            </Badge>
          ) : (
            <Badge appearance="tint" color="danger">
              {t("home.status.offline")}
            </Badge>
          )}
        </div>
        <div className={styles.row}>
          <Body1>{t("home.status.model")}</Body1>
          {configured === undefined ? (
            <Badge appearance="tint" color="subtle">
              {t("home.status.unknown")}
            </Badge>
          ) : configured ? (
            <Badge appearance="tint" color="success">
              {t("home.status.configured")}
            </Badge>
          ) : (
            <Badge appearance="tint" color="warning">
              {t("home.status.notConfigured")}
            </Badge>
          )}
        </div>
        {state.kind === "offline" && <Caption1 className={styles.hint}>{t("home.status.offlineHint")}</Caption1>}
        {configured === false && <Caption1 className={styles.hint}>{t("home.status.notConfiguredHint")}</Caption1>}
      </div>
    </Card>
  );
}
