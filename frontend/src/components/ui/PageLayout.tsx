"use client";

import { Body1, makeStyles, mergeClasses, Title2, tokens } from "@fluentui/react-components";
import type { ReactNode } from "react";

const useStyles = makeStyles({
  body: {
    width: "100%",
    maxWidth: "1200px",
    marginInline: "auto",
    boxSizing: "border-box",
    padding: `${tokens.spacingVerticalXXXL} ${tokens.spacingHorizontalXXL}`,
    display: "flex",
    flexDirection: "column",
    gap: tokens.spacingVerticalXL,
    flexGrow: 1,
    "@media (max-width: 639px)": { padding: `${tokens.spacingVerticalXL} ${tokens.spacingHorizontalL}` },
  },
  header: { display: "flex", alignItems: "flex-start", justifyContent: "space-between", gap: tokens.spacingHorizontalL, flexWrap: "wrap" },
  titles: { display: "flex", flexDirection: "column", gap: tokens.spacingVerticalXS },
  subtitle: { color: tokens.colorNeutralForeground3 },
});

/** Centred page column with the padding of the ValidatorHub pages. */
export function PageBody({ children, className }: { children: ReactNode; className?: string }) {
  const styles = useStyles();
  return <div className={mergeClasses(styles.body, className)}>{children}</div>;
}

/** Page title (h1), optional subtitle and actions on the right. */
export function PageHeader({ title, subtitle, actions }: { title: string; subtitle?: string; actions?: ReactNode }) {
  const styles = useStyles();
  return (
    <div className={styles.header}>
      <div className={styles.titles}>
        <Title2 as="h1">{title}</Title2>
        {subtitle && <Body1 className={styles.subtitle}>{subtitle}</Body1>}
      </div>
      {actions}
    </div>
  );
}
