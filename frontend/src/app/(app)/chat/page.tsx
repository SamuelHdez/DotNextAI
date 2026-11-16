import type { Metadata } from "next";
import { ChatWorkspace } from "@/components/savedChats/ChatWorkspace";
import { getRequestLocale } from "@/lib/i18n/server";
import { translate } from "@/lib/i18n/translate";

export async function generateMetadata(): Promise<Metadata> {
  return { title: translate(await getRequestLocale(), "chat.title") };
}

/** `/chat?c=<id>` opens a saved conversation; `/chat` starts a new one. */
export default async function ChatPage({ searchParams }: { searchParams: Promise<{ c?: string | string[] }> }) {
  const { c } = await searchParams;
  return <ChatWorkspace initialConversationId={typeof c === "string" && c ? c : null} />;
}
