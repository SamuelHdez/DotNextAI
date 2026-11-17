import { redirect } from "next/navigation";
import { chatHref } from "@/lib/chat/savedChats";

/** The chat moved to `/` on 2026-11-17; old `/chat` and `/chat?c=<id>` links keep working. */
export default async function LegacyChatPage({ searchParams }: PageProps<"/chat">) {
  const { c } = await searchParams;
  redirect(typeof c === "string" && c ? chatHref(c) : "/");
}
