import { AppShell } from "@/components/shell/AppShell";

/** Every page lives inside the shell (header + side navigation), which persists across client-side navigations. */
export default function AppLayout({ children }: LayoutProps<"/">) {
  return <AppShell>{children}</AppShell>;
}
