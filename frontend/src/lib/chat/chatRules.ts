/** Same limits as the backend's ChatRules (docs/specs/feature-llm-chat.md#rules); checked here before sending. */
export const MAX_QUESTION_LENGTH = 20_000;
export const MAX_CONVERSATION_QUESTIONS = 50;

export type QuestionCheck = { ok: true; question: string } | { ok: false; reason: "empty" } | { ok: false; reason: "tooLong"; length: number };

export function checkQuestion(text: string): QuestionCheck {
  const question = text.trim();
  if (question.length === 0) return { ok: false, reason: "empty" };
  if (question.length > MAX_QUESTION_LENGTH) return { ok: false, reason: "tooLong", length: question.length };
  return { ok: true, question };
}
