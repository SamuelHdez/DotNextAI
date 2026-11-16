"use client";

import {
  makeStyles,
  mergeClasses,
  NavDrawer,
  NavDrawerBody,
  NavItem,
  NavSectionHeader,
  tokens,
  Tooltip,
  type NavDrawerProps,
} from "@fluentui/react-components";
import { bundleIcon, Chat20Filled, Chat20Regular, Home20Filled, Home20Regular } from "@fluentui/react-icons";
import { usePathname, useRouter } from "next/navigation";
import type { MouseEvent, ReactElement } from "react";
import { useI18n } from "@/lib/i18n/I18nProvider";
import type { MessageKey } from "@/lib/i18n/messages/en";

const HomeIcon = bundleIcon(Home20Filled, Home20Regular);
const ChatIcon = bundleIcon(Chat20Filled, Chat20Regular);

export const NAV_EXPANDED_WIDTH = "260px";
export const NAV_COLLAPSED_WIDTH = "56px";

interface NavLink {
  value: string;
  label: MessageKey;
  href: string;
  icon: ReactElement;
}

interface NavGroup {
  name: MessageKey;
  links: NavLink[];
}

/** Navigation of the app. Later backlog steps add pages here (e.g. image generation in step 09). */
export const NAV_GROUPS: NavGroup[] = [
  {
    name: "nav.section.workspace",
    links: [
      { value: "home", label: "nav.home", href: "/", icon: <HomeIcon /> },
      { value: "chat", label: "nav.chat", href: "/chat", icon: <ChatIcon /> },
    ],
  },
];

export function selectedValueFor(pathname: string, groups: NavGroup[] = NAV_GROUPS): string {
  const links = groups.flatMap((group) => group.links);
  const match = links
    .filter((link) => (link.href === "/" ? pathname === "/" : pathname === link.href || pathname.startsWith(`${link.href}/`)))
    .sort((a, b) => b.href.length - a.href.length)[0];
  return match?.value ?? "";
}

const useStyles = makeStyles({
  drawer: {
    height: "100%",
    width: NAV_EXPANDED_WIDTH,
    backgroundColor: tokens.colorNeutralBackground1,
    borderRight: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralStroke2}`,
    "@media (prefers-reduced-motion: no-preference)": {
      transitionProperty: "width",
      transitionDuration: tokens.durationNormal,
      transitionTimingFunction: tokens.curveEasyEase,
    },
  },
  // Icon-only rail. Fluent's Nav has no built-in collapsed layout, so labels stay in the DOM (accessible names)
  // and are only hidden visually; tooltips on the right reveal them, as the Fluent 2 guidance recommends.
  rail: { width: NAV_COLLAPSED_WIDTH, minWidth: NAV_COLLAPSED_WIDTH, overflowX: "hidden" },
  body: { paddingBlock: tokens.spacingVerticalM },
  // NavItem paints itself with colorNeutralBackground4 (Fluent's default drawer colour). On our white drawer
  // that made every item look highlighted, so items are transparent and only hover/selected get a fill.
  item: {
    backgroundColor: tokens.colorSubtleBackground,
    ":hover": { backgroundColor: tokens.colorSubtleBackgroundHover },
    ":active": { backgroundColor: tokens.colorSubtleBackgroundPressed },
    '&[aria-current="page"]': { backgroundColor: tokens.colorSubtleBackgroundSelected, fontWeight: tokens.fontWeightSemibold },
  },
  railBody: { paddingInline: tokens.spacingHorizontalXS },
  // Fluent's selection pipe sits in the item's left padding, which the rail removes, so it would overlap the icon.
  railItem: { justifyContent: "center", paddingInline: 0, columnGap: 0, "::after": { display: "none" } },
  visuallyHidden: {
    position: "absolute",
    width: "1px",
    height: "1px",
    overflow: "hidden",
    clip: "rect(0 0 0 0)",
    whiteSpace: "nowrap",
  },
});

interface SideNavProps {
  type: NavDrawerProps["type"];
  open: boolean;
  /** Icon-only rail (inline drawer only). */
  collapsed?: boolean;
  onOpenChange?: (open: boolean) => void;
  /** Called after a link is chosen (used to close the overlay drawer on small screens). */
  onNavigate?: () => void;
  className?: string;
}

export function SideNav({ type, open, collapsed = false, onOpenChange, onNavigate, className }: SideNavProps) {
  const styles = useStyles();
  const { t } = useI18n();
  const router = useRouter();
  // usePathname() is only null outside the App Router (e.g. isolated component tests).
  const pathname = usePathname() ?? "/";

  function navigate(event: MouseEvent, href: string) {
    // Keep native behaviour for new-tab / new-window clicks; use client-side navigation otherwise.
    if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
    event.preventDefault();
    router.push(href);
    onNavigate?.();
  }

  return (
    <NavDrawer
      type={type}
      open={open}
      onOpenChange={(_, data) => onOpenChange?.(data.open)}
      selectedValue={selectedValueFor(pathname)}
      aria-label={t("nav.label")}
      data-collapsed={collapsed}
      className={mergeClasses(styles.drawer, collapsed && styles.rail, className)}
    >
      <NavDrawerBody className={mergeClasses(styles.body, collapsed && styles.railBody)}>
        {NAV_GROUPS.map((group) => (
          <div key={group.name} role="group" aria-label={t(group.name)}>
            {!collapsed && <NavSectionHeader>{t(group.name)}</NavSectionHeader>}
            {group.links.map((link) => {
              const label = t(link.label);
              const item = (
                <NavItem
                  key={link.value}
                  value={link.value}
                  href={link.href}
                  icon={link.icon}
                  className={mergeClasses(styles.item, collapsed && styles.railItem)}
                  onClick={(event) => navigate(event, link.href)}
                >
                  {collapsed ? <span className={styles.visuallyHidden}>{label}</span> : label}
                </NavItem>
              );
              return collapsed ? (
                // The visible name comes from the tooltip; the accessible name stays on the link itself.
                <Tooltip key={link.value} content={label} relationship="inaccessible" positioning="after" withArrow>
                  {item}
                </Tooltip>
              ) : (
                item
              );
            })}
          </div>
        ))}
      </NavDrawerBody>
    </NavDrawer>
  );
}
