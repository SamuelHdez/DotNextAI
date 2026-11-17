import type { Metadata } from "next";
import { ChatWorkspace } from "@/components/savedChats/ChatWorkspace";
import { getRequestLocale } from "@/lib/i18n/server";
import { translate } from "@/lib/i18n/translate";

export async function generateMetadata(): Promise<Metadata> {
  return { title: translate(await getRequestLocale(), "chat.title") };
}

/** The start page is the chat: `/` starts a new conversation and `/?c=<id>` opens a saved one. */
export default async function ChatPage({ searchParams }: PageProps<"/">) {
  const { c } = await searchParams;
  return <ChatWorkspace initialConversationId={typeof c === "string" && c ? c : null} />;
}
