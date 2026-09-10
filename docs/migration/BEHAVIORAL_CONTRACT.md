# Behavioral Contract: Jadwal Migration

**Date**: 2026-09-10  
**Project**: Jadwal  
**Status**: Frozen Characterization Contract

This document freezes all behavioral invariants and expected outcomes for the C# / .NET / Avalonia implementation. All test suites in the new platform must conform to the behaviors documented here.

---

## 1. Timetable Import & Normalization

1. **Input Payloads**:
   - Accepts JSON containing `academicYear`, `weekNumber`, and an array of `entries` (or `periods`).
   - Each entry contains: `day`, `date`, `period`, `startTime`, `endTime`, `subject`, `details`.
2. **Determinism**:
   - Duplicate imports of the same payload must be completely idempotent.
   - Slot ID formula: `{date}_{periodName.replace(' ', '_')}`.
3. **Break Insertion Rules**:
   - A timeline gap between consecutive periods $\ge 10$ minutes generates a synthetic `breakBlock`.
   - Before 08:00 $\rightarrow$ `Morning Preparation`.
   - 10:00 – 12:00 $\rightarrow$ `Recess`.
   - 12:00 – 14:00 $\rightarrow$ `Lunch & Namaz Break`.
   - After 14:00 $\rightarrow$ `Afternoon Break`.

---

## 2. Schedule Partitioning & Day Rules

| Day Category | Periods | Row 1 Content | Row 2 Content |
|---|---|---|---|
| **Mon – Thu** | 10 periods | Periods 1–5 + Morning Prep & Recess | Periods 6–10 + Lunch/Namaz Break |
| **Friday** | 9 periods (no PT) | Periods 2–5 + Recess | Periods 6–10 + Extended Jumua Break |
| **Saturday** | 8 periods (half-day) | Periods 1–4 + Recess (10:35–10:55) | Periods 5–8 (concludes 13:15) |
| **Sunday** | 0 periods | Empty day view | Empty |

---

## 3. Class Live Status Progression

For current time $T$ relative to class start $S$ and end $E$:
- $T \ge E$: `.completed`
- $S \le T < E$: `.inProgress`
- $S - 15\text{m} \le T < S$: `.startingSoon(minutesRemaining)`
- $T < S - 15\text{m}$: `.upcoming`

---

## 4. Timetable Change Detection & Task Discrepancies

When diffing `OldSnapshot` vs `NewSnapshot`:
1. **Change Types**:
   - `ADDED`: New period slot.
   - `REMOVED`: Period slot no longer present in new timetable.
   - `TIME_CHANGED`: Same subject & slot, but `startTime` or `endTime` altered.
   - `SUBJECT_CHANGED`: Period slot has a different subject name.
   - `TEACHER_CHANGED`: Same subject, updated teacher details.
   - `ROOM_CHANGED`: Location altered.
   - `CANCELLED`: Explicitly marked cancelled.
2. **Task Protection Invariant**:
   - Tasks linked to a period whose subject changes MUST receive a `TaskDiscrepancy`.
   - Tasks are NEVER auto-deleted or silently retitled.
   - Discrepancy actions:
     - `AdoptNewSubject`: sets `linkedSubject = newSubject`, resets discrepancy.
     - `KeepOriginalContext`: keeps original subject, marks discrepancy dismissed.
     - `Dismiss`: dismisses notification.

---

## 5. Security Invariants

- Secret storage (JWT tokens, credentials) must strictly use OS secure keychains (Windows Credential Manager / macOS Keychain).
- Zero tokens or passwords in console logs, error messages, or crash dumps.
- All diagnostics must redact bearer tokens.
