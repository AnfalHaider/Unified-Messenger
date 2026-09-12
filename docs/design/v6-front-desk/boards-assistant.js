// ---- assistant ------------------------------------------------------------------------------------

boards.push({
  group: 'assistant', phase: 'Phase 5 · Assistant', order: 1,
  title: 'Ask the app',
  note: 'Questions in plain words, answered by a model running on this PC from a summary the app builds. Every answer sits beside the real figures it used, taken straight from the app, so a wrong sentence is visible at a glance. It can open a chat for you; it has no way to send one.',
  html: app({ active: 'assistant' }, `<main class="main" style="gap:16px;max-width:1180px">
    <div class="headline"><div><h1>Assistant</h1><p>Runs on this PC with Gemma 3, 4B. Nothing you ask or it reads leaves the machine.</p></div>
      <div class="actions"><span class="chip ok">${ic('shield', 12)}On this PC</span><button class="btn quiet">New conversation</button></div></div>
    <div class="chat" style="flex:1;min-height:0">
      <div class="q-bub">Who has waited longest, and is anyone at F-11 being ignored?</div>
      <div class="a-bub"><div>
        <p><b>Zainab T. at DHA-2 has waited longest</b>, 71 minutes, about confirming a 3pm booking.</p>
        <p>At F-11, four customers are past the 15-minute target. Two of them are on Instagram, which has been slower than WhatsApp all week:</p>
        <ol><li>Ayesha K., Instagram, 52 min, asking about a bridal package</li><li>Sara M., WhatsApp, 38 min, wants to move her 5pm</li><li>Rabia N., WhatsApp, 24 min, sent a voice message</li><li>Hira A., WhatsApp, 17 min, asking for a Saturday slot</li></ol>
        <div style="display:flex;gap:8px;margin-top:12px"><button class="btn">${ic('open', 14)}Open Zainab’s chat</button><button class="btn">${ic('open', 14)}Open Ayesha’s chat</button></div>
      </div>
      <div class="evidence"><h5>Figures used, from the app</h5>
        <div><span>Waiting now</span><b>19</b></div><div><span>Past target</span><b style="color:var(--late)">7</b></div><div><span>Longest wait</span><b>71 min</b></div><div><span>F-11 past target</span><b style="color:var(--late)">4</b></div><div><span>F-11 Instagram, median this week</span><b>22 min</b></div><div><span>Read</span><b>20 s ago</b></div>
        <span class="sub" style="margin-top:4px">If these and the answer disagree, the figures are right.</span></div>
      </div>
      <div class="q-bub">Why was Saturday so slow?</div>
      <div class="a-bub"><div><p>Saturday had the most messages of the week, 206, and most arrived between 3 and 5 pm. F-11 answered 61% of those on time; the other two stayed above 85%.</p><p class="sub" style="margin:0">That matches the busy-hours map in Reports.</p></div>
        <div class="evidence"><h5>Figures used</h5><div><span>Saturday messages</span><b>206</b></div><div><span>F-11 on time, 3–5 pm</span><b style="color:var(--late)">61%</b></div><div><span>DHA-2, same hours</span><b>86%</b></div><div><span>Men DHA-2, same hours</span><b>88%</b></div></div></div>
    </div>
    <div class="suggest"><span>Which account is slowest this week?</span><span>Who wrote overnight?</span><span>How many missed calls are not returned?</span><span>Summarise today for the manager</span></div>
    <div class="ask" style="margin-bottom:22px">${ic('spark', 16)}Ask about waiting, replies, reviews or calls…<span style="margin-left:auto"><kbd>Enter</kbd></span></div>
  </main>`),
});

boards.push({
  group: 'assistant', phase: 'Phase 5 · Assistant · “How should I reply?”', order: 2,
  title: 'Help with a reply, never sending it',
  note: 'On a docked chat, “Suggest a reply” reads only that conversation’s last few messages and offers two drafts in the business’s own tone. Copying puts it on the clipboard; the person at the desk pastes it into WhatsApp and presses send themselves.',
  html: app({ active: 'line' }, `<div class="split" style="grid-template-columns:minmax(0,1fr) 400px">
    <section class="dock" aria-label="F-11 Instagram" style="border-left:0">
      <div class="dock-bar">${ic('ig', 18)}<span class="who"><b>Ayesha K.</b><span>F-11 Instagram · waiting 52 min, 37 past target</span></span><div class="actions"><button class="btn">${ic('check', 14)}Handled</button><button class="btn">${ic('snooze', 14)}Snooze</button></div></div>
      <div class="page-mock" style="grid-template-columns:1fr"><div class="pm-chat" style="padding:28px 60px">
        <div class="bub">Assalam o alaikum, I’m getting married on 14 November<small>3:51 pm</small></div>
        <div class="bub">Bridal package price? And do you do the trial before?<small>3:52 pm</small></div>
        <div class="bub">Also can my sister and mother book with me same day<small>3:58 pm</small></div>
        <div class="pm-compose"></div></div></div>
    </section>
    <aside class="cust" style="gap:14px"><div style="display:flex;align-items:center;gap:8px">${ic('spark', 16)}<h3 style="margin:0;font:650 17px/1.2 var(--display)">Suggested replies</h3></div>
      <p class="sub" style="margin:0">From Ayesha’s last 3 messages and your saved price list. Nothing else was read.</p>
      ${[['Warm and complete', 'Walaikum assalam Ayesha, congratulations! Our bridal package is Rs 85,000 and includes a trial two weeks before. We can book your sister and mother on the same day too. Shall I hold 14 November for you?'], ['Short', 'Congratulations! Bridal is Rs 85,000 with a trial included, and family bookings on the same day are no problem. Which time suits you?']].map(([t, body], i) => `<div style="border:1px solid ${i === 0 ? 'var(--ink)' : 'var(--line-2)'};border-radius:10px;padding:12px;background:var(--raised);display:grid;gap:8px"><b style="font-weight:600;font-size:13px">${t}</b><span style="font-size:13.5px;line-height:1.55">${body}</span><div style="display:flex;gap:6px"><button class="btn ${i === 0 ? 'primary' : ''}" style="height:28px">${ic('copy', 13)}Copy</button><button class="btn quiet" style="height:28px">Edit</button></div></div>`).join('')}
      <div class="panel" style="padding:10px 12px;display:grid;grid-template-columns:auto 1fr;gap:8px;align-items:start">${ic('alert', 15)}<span class="sub">Check the price before sending. It came from the saved list, last edited 2 August.</span></div>
      <div class="sub" style="display:flex;gap:6px;align-items:center;margin-top:auto">${ic('shield', 13)}Drafted on this PC. The app cannot send messages.</div>
    </aside>
  </div>`),
});

boards.push({
  group: 'assistant', phase: 'Phase 5 · Assistant · off by default', order: 3,
  title: 'Turning the assistant on',
  note: 'Off until someone chooses it. The app checks this PC’s memory and suggests a model that will run comfortably, says exactly how big the download is, and shows it arriving. With the assistant off, every other screen works exactly the same.',
  html: app({ active: 'settings' }, `<main class="main">
    <div class="headline"><div><h1>Settings</h1><p>Kept on this PC.</p></div></div>
    <div class="settings">
      <nav class="stabs"><button>Look and reading</button><button>Opening hours</button><button>Notifications</button><button aria-current="page">Assistant</button><button>Workspace</button><button>Privacy</button><button>About</button></nav>
      <div style="display:grid;gap:22px;align-content:start">
        <div class="sgroup"><h3>Assistant</h3><p>Answers questions about waiting, replies, reviews and calls, and drafts replies to copy. It runs entirely on this PC.</p>
          <div class="panel" style="padding:0">
            <div class="srow"><span><b>Use the assistant</b><span>Off by default. Nothing is downloaded until this is on.</span></span><span class="toggle"></span></div>
            <div class="srow"><span><b>Model</b><span>This PC has 16 GB of memory. Gemma 3, 4B is the largest that stays quick here.</span></span><div class="seg"><button>Small, 1B</button><button aria-pressed="true">Balanced, 4B</button><button>Large, 12B</button></div></div>
          </div></div>
        <div class="panel" style="display:grid;gap:10px"><div style="display:flex;justify-content:space-between;align-items:baseline"><b style="font-weight:600">Downloading Gemma 3, 4B</b><span class="sub num">2.1 of 3.3 GB · about 4 minutes left</span></div>
          <div class="progress"><i style="width:64%"></i></div>
          <div style="display:flex;gap:18px" class="sub"><span>${ic('check', 13, 2)} Ollama engine installed</span><span>${ic('download', 13)} Model downloading</span><span style="opacity:.6">Ready to answer</span></div>
          <div style="display:flex;gap:8px"><button class="btn">Pause</button><button class="btn quiet">Cancel and remove</button></div></div>
        <div class="sgroup"><h3>What it can see</h3>
          <div class="panel" style="padding:0">
            <div class="srow"><span><b>Figures and waiting customers</b><span>Counts, times, names and previews the app already shows you.</span></span><span class="toggle"></span></div>
            <div class="srow"><span><b>A chat’s last messages, when you ask for a reply</b><span>Only that one conversation, only when you press Suggest a reply.</span></span><span class="toggle"></span></div>
            <div class="srow"><span><b>Customer notes</b><span>Off: notes stay out of the assistant unless you allow it.</span></span><span class="toggle off"></span></div>
          </div></div>
      </div>
    </div>
  </main>`),
});
