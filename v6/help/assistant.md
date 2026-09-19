# Assistant

Ask a question about who is waiting and get the answer in the app's own figures. A local model on this PC only picks which of the app's figures answer your question; it does not write the answer, so it cannot get a number wrong.

![Assistant](shot:assistant)

## Switching it on

The assistant is **off** until you switch it on in [Settings](help:settings) › Assistant. It runs through **Ollama**, a free program that runs models on this PC. The app uses an Ollama already on the PC; if there is none, Settings offers to download it (about 1.2 GB), and then the model (about 3.3 GB for the balanced one). Nothing is downloaded until you press the button.

## Asking

- Type a question, or press one of the suggestions, then **Ask**.
- It answers from the figures the app has worked out: who is waiting and for how long, who is past the target, each location, each channel, accounts that need signing in, replies measured today.
- Each figure it picks is checked against your question before it is shown. If none fits, it says **"The app doesn't have that figure."** instead of showing the nearest one. Reviews, calls, sales and past days are not covered yet.
- An answer takes a few seconds, longer on a slower PC.
- When the answer names a customer who is waiting, a button opens their chat.
- Beside each answer, **At a glance, from the app** shows how many are waiting, how many are past the target and the longest wait.

## What stays private

- Everything happens on this PC. Nothing is sent to any online service.
- The conversation is not saved: it is gone when you leave the app, and it is never written to the log.
- The assistant cannot send messages or change anything.
