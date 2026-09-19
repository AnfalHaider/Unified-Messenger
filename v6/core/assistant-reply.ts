// Suggest a reply: the chat's messages turned into a request for two drafts, and the model's answer turned back into
// drafts. The owner copies one, edits it, and sends it themselves; the app never sends anything.
//
// Owner decision 2026-09-19: the whole open conversation may be read (what WhatsApp has loaded for it), on this PC
// only. A small model's window is limited, so the newest messages are kept and the oldest dropped to fit.

export interface ChatLine { fromMe: boolean; text: string; t: number }
export interface Draft { title: string; body: string }

/** Roughly what fits beside the instructions in an 8k-token window, with room for the answer. */
export const TRANSCRIPT_BUDGET = 12_000;

/** The newest messages that fit, oldest first, as "Customer:" and "Us:" lines. */
export function transcript(lines: ChatLine[], budget = TRANSCRIPT_BUDGET): { text: string; used: number } {
  const kept: string[] = [];
  let size = 0;
  for (let i = lines.length - 1; i >= 0; i--) {
    const line = `${lines[i].fromMe ? 'Us' : 'Customer'}: ${lines[i].text}`;
    if (size + line.length + 1 > budget && kept.length) break;
    kept.unshift(line);
    size += line.length + 1;
  }
  return { text: kept.join('\n'), used: kept.length };
}

/** The request to the local model. `business` is the account's name as the owner gave it, so the draft can sign off
 *  as the business without inventing a name. */
export function replyPrompt(lines: ChatLine[], business: string): { system: string; user: string; used: number } {
  const { text, used } = transcript(lines);
  const system = [
    `You draft WhatsApp replies for a business ("${business}") to a customer. You never send anything; a person reads, edits and sends the draft.`,
    'Reply to what the customer last asked or said, in the same language the customer writes in.',
    'Use only what the conversation says. Never invent prices, times, availability, addresses or promises; where one is needed, write it as [price], [time], [date] or [address] for the person to fill in.',
    'Write exactly two drafts, in this form and nothing else:',
    'WARM:',
    '<a friendly, complete reply of two to four sentences>',
    'SHORT:',
    '<a one-sentence reply>',
  ].join('\n');
  return { system, user: `The conversation so far, oldest first:\n${text}`, used };
}

/** The model's answer as drafts. A model that ignores the form still gives one draft, never none. */
export function parseDrafts(answer: string): Draft[] {
  const warm = /WARM:\s*([\s\S]*?)(?:\n\s*SHORT:|$)/i.exec(answer)?.[1]?.trim();
  const short = /SHORT:\s*([\s\S]*)$/i.exec(answer)?.[1]?.trim();
  const drafts: Draft[] = [];
  if (warm) drafts.push({ title: 'Warm and complete', body: warm });
  if (short) drafts.push({ title: 'Short', body: short });
  if (!drafts.length && answer.trim()) drafts.push({ title: 'Draft', body: answer.trim() });
  return drafts;
}
