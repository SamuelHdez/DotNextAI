"use client";

import { makeStyles, tokens } from "@fluentui/react-components";
import { useEffect, useState, type ReactNode } from "react";
import { AppHeader } from "@/components/shell/AppHeader";
import { HEADER_HEIGHT, NAV_OVERLAY_MEDIA, NAV_OVERLAY_QUERY } from "@/components/shell/navBreakpoint";
import { SideNav } from "@/components/shell/SideNav";
import { useI18n } from "@/lib/i18n/I18nProvider";

const useStyles = makeStyles({
  skipLink: {
    position: "absolute",
    left: tokens.spacingHorizontalM,
    top: "-100px",
    zIndex: 2,
    padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalM}`,
    backgroundColor: tokens.colorNeutralBackground1,
    color: tokens.colorBrandForegroundLink,
    borderRadius: tokens.borderRadiusMedium,
    boxShadow: tokens.shadow8,
    ":focus": { top: tokens.spacingVerticalS },
  },
  body: { display: "flex", flexGrow: 1, minHeight: 0, backgroundColor: tokens.colorNeutralBackground2 },
  // Inline drawer on wide screens; below the breakpoint the overlay drawer is used instead.
  inlineNav: {
    position: "sticky",
    top: HEADER_HEIGHT,
    height: `calc(100dvh - ${HEADER_HEIGHT})`,
    flexShrink: 0,
    [NAV_OVERLAY_MEDIA]: { display: "none" },
  },
  content: { flexGrow: 1, minWidth: 0, display: "flex", flexDirection: "column", outlineStyle: "none" },
});

function isOverlayViewport(): boolean {
  return typeof window !== "undefined" && window.matchMedia(NAV_OVERLAY_QUERY).matches;
}

/**
 * Header + side navigation around every page (same structure as ValidatorHub). Layouts persist across client-side
 * navigations, so the shell and its open/collapsed state stay mounted and only the page below is swapped.
 */
export function AppShell({ children }: { children: ReactNode }) {
  const styles = useStyles();
  const { t } = useI18n();
  // Inline navigation starts expanded on wide screens (collapsing leaves an icon rail);
  // the overlay starts closed on narrow ones.
  const [inlineExpanded, setInlineExpanded] = useState(true);
  const [overlayOpen, setOverlayOpen] = useState(false);
  const [overlayViewport, setOverlayViewport] = useState(false);

  // Track which drawer is in use; close the overlay if the window grows past the breakpoint.
  useEffect(() => {
    const query = window.matchMedia(NAV_OVERLAY_QUERY);
    const sync = () => {
      setOverlayViewport(query.matches);
      if (!query.matches) setOverlayOpen(false);
    };
    sync();
    query.addEventListener("change", sync);
    return () => query.removeEventListener("change", sync);
  }, []);

  function toggleNavigation() {
    if (isOverlayViewport()) setOverlayOpen((open) => !open);
    else setInlineExpanded((expanded) => !expanded);
  }

  return (
    <>
      <a href="#main-content" className={styles.skipLink}>
        {t("a11y.skipToContent")}
      </a>
      <AppHeader navigationOpen={overlayViewport ? overlayOpen : inlineExpanded} onToggleNavigation={toggleNavigation} />
      <div className={styles.body}>
        <SideNav type="inline" open collapsed={!inlineExpanded} className={styles.inlineNav} />
        <SideNav type="overlay" open={overlayOpen} onOpenChange={setOverlayOpen} onNavigate={() => setOverlayOpen(false)} />
        <main id="main-content" tabIndex={-1} className={styles.content}>
          {children}
        </main>
      </div>
    </>
  );
}
