"use client";

import { Badge, Body1, Caption1, Card, CardHeader, makeStyles, Subtitle1, Subtitle2, tokens } from "@fluentui/react-components";
import { BACKLOG, type StepStatus } from "@/lib/backlog";
import { useI18n } from "@/lib/i18n/I18nProvider";

const useStyles = makeStyles({
  phases: { display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: tokens.spacingHorizontalXL },
  phase: { display: "flex", flexDirection: "column", gap: tokens.spacingVerticalS },
  list: { listStyle: "none", margin: 0, padding: 0, display: "flex", flexDirection: "column", gap: tokens.spacingVerticalS },
  step: { display: "flex", alignItems: "center", justifyContent: "space-between", gap: tokens.spacingHorizontalS },
  number: { color: tokens.colorBrandForeground1, fontWeight: tokens.fontWeightSemibold, marginInlineEnd: tokens.spacingHorizontalS },
  description: { color: tokens.colorNeutralForeground3 },
});

const STATUS_BADGE: Record<StepStatus, { color: "success" | "brand" | "subtle"; label: "home.backlog.done" | "home.backlog.current" | "home.backlog.planned" }> = {
  done: { color: "success", label: "home.backlog.done" },
  current: { color: "brand", label: "home.backlog.current" },
  planned: { color: "subtle", label: "home.backlog.planned" },
};

/** The ten steps of the course and how far DotNextAI has got. */
export function BacklogCard() {
  const styles = useStyles();
  const { t } = useI18n();

  return (
    <Card>
      <CardHeader
        header={<Subtitle1 as="h2">{t("home.backlog.title")}</Subtitle1>}
        description={<Caption1 className={styles.description}>{t("home.backlog.description")}</Caption1>}
      />
      <div className={styles.phases}>
        {BACKLOG.map((phase) => (
          <section key={phase.title} className={styles.phase} aria-label={t(phase.title)}>
            <Subtitle2 as="h3">{t(phase.title)}</Subtitle2>
            <ol className={styles.list}>
              {phase.steps.map((step) => {
                const badge = STATUS_BADGE[step.status];
                return (
                  <li key={step.number} className={styles.step}>
                    <Body1>
                      <span className={styles.number}>{step.number}</span>
                      {t(step.title)}
                    </Body1>
                    <Badge appearance="tint" color={badge.color}>
                      {t(badge.label)}
                    </Badge>
                  </li>
                );
              })}
            </ol>
          </section>
        ))}
      </div>
    </Card>
  );
}
