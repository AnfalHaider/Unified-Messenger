---
id: v6-hours-come-from-hoursfor
date: 2026-09-14
agent: claude
title: A location's hours must be read through hoursFor, never location.hours, or holidays silently stop counting
triggers: [opening hours, business hours, holidays, closedDates, elapsedBusinessMinutes, isOpen, location.hours, hoursFor, waited, wait not pausing, week, workingDays]
files: [v6/core/config.ts, v6/core/business-hours.ts, v6/app/view-model.ts]
cost: Found while wiring 4.4; four call sites read location.hours directly and would have ignored every holiday.
status: live
---

Holidays live in `config.holidays`, apart from each location, because one holiday usually applies to several locations. The rules only see them as `closedDates` on a `BusinessHours`, and `hoursFor(config, locationName)` is the one place that merges the two. Reading `config.locations.find(...).hours` gives hours with no closed dates, so a wait keeps growing through a holiday and nothing looks wrong. Every elapsed-wait, open-now and backlog calculation must go through `hoursFor`. Two more rules of the model: hours written by v5 (`openMinutes`, `closeMinutes`, `workingDays`) have no `week` and are still honoured, so code must use `windowFor(hours, date)` rather than read `week` directly; and hours that can never open (every day closed) count as switched off, as v5 did, because a clock that never runs would hide every waiting customer.
