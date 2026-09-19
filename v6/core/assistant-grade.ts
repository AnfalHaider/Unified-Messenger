// Grading the assistant's answers for the test set (roadmap 5.5). Owner's pass mark: 30 of 30, and an answer that
// quotes a number the facts do not contain fails, whatever else it gets right.
//
// Numbers are compared as whole numbers, never as text: the first grader passed "How many were marked handled?" on
// an answer about 82%, because "2" is inside "82".

export interface Check {
  question: string;
  /** Every one of these must appear in the answer. A number must appear as that whole number; words, case ignored. */
  must?: string[];
  /** At least one of these must appear, for answers that can rightly be worded more than one way. */
  anyOf?: string[];
  /** None of these may appear: a wrong customer named beside the right one is a wrong answer. */
  mustNot?: string[];
  /** The facts do not cover it: the answer must say the app does not have that figure, and quote no number. */
  notCovered?: boolean;
}

export interface Grade { pass: boolean; why: string }

/** Every number in a text, as written: "2", "15", "82", "3:41", "1,234". A trailing full stop is not part of it. */
export const numbersIn = (text: string): string[] => text.match(/\d+(?:[:.,]\d+)*/g) ?? [];

const isNumber = (s: string) => /^\d+(?:[:.,]\d+)*$/.test(s);

/** Whether the answer contains this: a whole number among its numbers, or words anywhere (case ignored). "12 min"
 *  is words with a number in them: its number must be a whole number of the answer, and its words must follow. */
function contains(answer: string, wanted: string): boolean {
  if (isNumber(wanted)) return numbersIn(answer).includes(wanted);
  const lead = /^(\d+(?:[:.,]\d+)*)\s+(.*)$/.exec(wanted);
  if (lead) return new RegExp(`(^|[^\\d.,:])${lead[1].replace(/[.,:]/g, '\\$&')}\\s+${lead[2].replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}`, 'i').test(answer);
  return answer.toLowerCase().includes(wanted.toLowerCase());
}

/** A number counts as invented when neither the facts nor the question contain it. */
export function inventedNumbers(answer: string, facts: string, question: string): string[] {
  // The facts' own labels ("F12.") and list numbers ("7.") are not figures, and must not excuse a made-up 12 or 7.
  const figures = facts.replace(/^(F?\d+)\. /gm, '');
  const known = new Set([...numbersIn(figures), ...numbersIn(question)]);
  return numbersIn(answer).filter((n) => !known.has(n) && !known.has(n.replace(/,/g, '')));
}

export function grade(answer: string, check: Check, facts: string): Grade {
  if (check.notCovered) {
    if (!/doesn.?t have that figure|does not have that figure/i.test(answer)) return { pass: false, why: 'should have said the app does not have that figure' };
    if (numbersIn(answer).length) return { pass: false, why: `quoted a number for something the facts do not cover: ${numbersIn(answer).join(', ')}` };
    return { pass: true, why: 'said the app does not have it' };
  }
  const invented = inventedNumbers(answer, facts, check.question);
  if (invented.length) return { pass: false, why: `made-up figure: ${invented.join(', ')}` };
  const missing = (check.must ?? []).filter((m) => !contains(answer, m));
  if (missing.length) return { pass: false, why: `missing: ${missing.join(', ')}` };
  if (check.anyOf && !check.anyOf.some((m) => contains(answer, m))) return { pass: false, why: `none of: ${check.anyOf.join(' / ')}` };
  const wrong = (check.mustNot ?? []).filter((m) => contains(answer, m));
  if (wrong.length) return { pass: false, why: `should not say: ${wrong.join(', ')}` };
  return { pass: true, why: 'right' };
}
