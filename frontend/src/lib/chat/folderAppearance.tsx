import { tokens } from "@fluentui/react-components";
import {
  Airplane20Regular,
  Beaker20Regular,
  Book20Regular,
  Briefcase20Regular,
  Code20Regular,
  Folder20Regular,
  Heart20Regular,
  Home20Regular,
  Lightbulb20Regular,
  People20Regular,
  Rocket20Regular,
  Star20Regular,
  type FluentIcon,
} from "@fluentui/react-icons";
import type { FolderColor, FolderIcon } from "@/services/types";

/**
 * How folder icons and colours look (docs/specs/feature-saved-chats.md#rules). The keys come from the backend's
 * `FolderAppearance`; colours are theme tokens (Fluent's palette, foreground 2), so they follow light and dark mode.
 */
export const FOLDER_ICON_COMPONENTS: Record<FolderIcon, FluentIcon> = {
  folder: Folder20Regular,
  briefcase: Briefcase20Regular,
  star: Star20Regular,
  heart: Heart20Regular,
  book: Book20Regular,
  code: Code20Regular,
  lightbulb: Lightbulb20Regular,
  rocket: Rocket20Regular,
  people: People20Regular,
  home: Home20Regular,
  beaker: Beaker20Regular,
  airplane: Airplane20Regular,
};

export const FOLDER_COLOR_TOKENS: Record<FolderColor, string> = {
  default: tokens.colorNeutralForeground2,
  red: tokens.colorPaletteRedForeground2,
  orange: tokens.colorPaletteDarkOrangeForeground2,
  yellow: tokens.colorPaletteMarigoldForeground2,
  green: tokens.colorPaletteGreenForeground2,
  teal: tokens.colorPaletteTealForeground2,
  blue: tokens.colorPaletteBlueForeground2,
  purple: tokens.colorPalettePurpleForeground2,
  berry: tokens.colorPaletteBerryForeground2,
  pink: tokens.colorPalettePinkForeground2,
  brown: tokens.colorPaletteBrownForeground2,
};

/** The icon of a folder in its colour; an unknown key (a newer server) falls back to the default. */
export function FolderGlyph({ icon, color, className }: { icon: string; color: string; className?: string }) {
  const Icon = FOLDER_ICON_COMPONENTS[icon as FolderIcon] ?? Folder20Regular;
  return <Icon aria-hidden className={className} style={{ color: FOLDER_COLOR_TOKENS[color as FolderColor] ?? FOLDER_COLOR_TOKENS.default }} />;
}
