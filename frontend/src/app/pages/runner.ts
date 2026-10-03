import { Component, computed, effect, ElementRef, HostListener, inject, input, OnDestroy, OnInit, signal, viewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Api, AttemptItem, AttemptState, toApiError } from '../core/api';

type SaveState = 'idle' | 'saving' | 'saved' | 'error';

@Component({
  selector: 'app-runner',
  imports: [RouterLink],
  template: `
    <div class="page runner">
      @if (fatal()) {
        <div class="notice error" role="alert">{{ fatal() }}</div>
        <a routerLink="/my" class="btn secondary">Back to my exams</a>
      }
      @if (state(); as s) {
        <header class="status">
          <div class="where">
            <span class="code">{{ s.attempt.certificationCode }}</span>
            <span>{{ modeName() }}</span>
            @if (s.attempt.preview) { <span class="tag warn">Admin preview</span> }
            @if (s.attempt.classification === 'assisted') { <span class="tag warn" title="Solutions were shown during this attempt">Assisted</span> }
            @else { <span class="tag ok">Clean</span> }
          </div>
          <div class="progress" aria-live="off">
            Question {{ index() + 1 }} of {{ s.items.length }}, {{ answeredCount() }} answered
          </div>
          @if (remaining() !== null) {
            <div class="timer" [class.low]="remaining()! <= 300" role="timer" [attr.aria-label]="'Time left ' + timeText()">
              {{ timeText() }}
            </div>
          }
        </header>
        <p class="sr" aria-live="polite">{{ announce() }}</p>
        @if (offline()) {
          <div class="notice warn" role="status">Connection lost. Your last answer is not saved yet; it will be retried automatically.{{ remaining() !== null ? ' The timer keeps running.' : '' }}</div>
        }

        <div class="layout">
          <section class="question" aria-labelledby="q-stem">
            @if (current(); as q) {
              @if (q.scenario) {
                <aside class="scenario" aria-label="Scenario">
                  <h2>{{ q.scenario.title }}</h2>
                  <p>{{ q.scenario.text }}</p>
                </aside>
              }
              <div class="qhead">
                <span class="qnum">Question {{ q.position }}</span>
                <span class="small muted">{{ q.questionType === 'multiple_response' ? 'Select ' + q.selectCount : 'Select one' }}</span>
              </div>
              <p id="q-stem" class="stem" #stem tabindex="-1">{{ q.stem }}</p>

              <fieldset class="options" [attr.aria-describedby]="'q-stem'">
                <legend class="visually-hidden">Answer options for question {{ q.position }}</legend>
                @for (o of q.options; track o.id) {
                  <label class="opt" [class.chosen]="isSelected(q, o.id)" [class.right]="q.solution && o.isCorrect"
                         [class.wrong]="q.solution && !o.isCorrect && wasScored(q, o.id)">
                    <input [type]="q.questionType === 'multiple_response' ? 'checkbox' : 'radio'" [name]="'q' + q.position"
                           [checked]="isSelected(q, o.id)" [disabled]="closed() || (!isSelected(q, o.id) && atLimit(q))"
                           (change)="choose(q, o.id, $any($event.target).checked)" />
                    <span class="letter" aria-hidden="true">{{ o.label }}</span>
                    <span class="otext">
                      {{ o.text }}
                      @if (q.solution) {
                        <span class="verdict"><span class="visually-hidden">{{ o.isCorrect ? 'Correct option: ' : 'Not the best option: ' }}</span>{{ o.rationale }}</span>
                      }
                    </span>
                  </label>
                }
              </fieldset>
              @if (q.questionType === 'multiple_response' && !q.solution) {
                <p class="small muted">{{ selectionOf(q).length }} of {{ q.selectCount }} selected</p>
              }

              <div class="item-actions">
                @if (s.attempt.feedbackEnabled && !q.feedbackRevealed) {
                  <button type="button" class="btn" (click)="check(q)" [disabled]="!canCheck(q) || busy()">Check answer</button>
                  @if (selectionOf(q).length === 0) {
                    <button type="button" class="btn quiet" (click)="check(q, true)" [disabled]="busy()">Show solution without answering</button>
                  }
                  @if (q.questionType === 'single_choice') {
                    <label class="check small"><input type="checkbox" [checked]="checkOnSelect()" (change)="checkOnSelect.set($any($event.target).checked)" /> Check as soon as I select</label>
                  }
                }
                <button type="button" class="btn secondary" (click)="toggleFlag(q)" [attr.aria-pressed]="q.flagged">
                  {{ q.flagged ? 'Remove review mark' : 'Mark for review' }}
                </button>
                <span class="save small" [class.err]="saveState() === 'error'" aria-live="polite">{{ saveLabel() }}</span>
              </div>

              @if (q.solution; as sol) {
                <section class="feedback" [class.ok]="sol.isCorrect" [class.bad]="sol.isCorrect === false" aria-live="polite" aria-labelledby="fb-h">
                  <h3 id="fb-h">
                    @if (sol.isCorrect === true) { Correct. } @else if (sol.isCorrect === false) { Not correct. } @else { Not answered. }
                  </h3>
                  @if (sol.isCorrect !== true) { <p>The correct answer is {{ correctLabels(q) }}.</p> }
                  <p>{{ sol.explanation }}</p>
                  @if (sol.sources.length) {
                    <p class="small">Study: @for (src of sol.sources; track src.id; let last = $last) { <a [href]="src.url" target="_blank" rel="noopener noreferrer">{{ src.title }}</a>{{ last ? '' : ', ' }} }</p>
                  }
                  <p class="small muted">You can still change your selection to learn; it is saved separately and does not change the score.</p>
                </section>
              }

              <nav class="pager" aria-label="Question navigation">
                <button type="button" class="btn secondary" (click)="go(index() - 1)" [disabled]="index() === 0">Previous</button>
                <button type="button" class="btn secondary" (click)="go(index() + 1)" [disabled]="index() === s.items.length - 1">Next</button>
              </nav>
            }
          </section>

          <aside class="side" aria-label="Question map and attempt controls">
            <h2 class="side-h">Answer sheet</h2>
            <ol class="map">
              @for (it of s.items; track it.position; let i = $index) {
                <li>
                  <button type="button" class="bubble" [class.current]="i === index()" [class.answered]="selectionOf(it).length > 0"
                          [class.flagged]="it.flagged" [attr.aria-current]="i === index() ? 'step' : null"
                          [attr.aria-label]="'Question ' + it.position + ', ' + mapState(it)" (click)="go(i)">{{ it.position }}</button>
                </li>
              }
            </ol>
            <p class="legend small"><span class="k answered"></span> answered <span class="k flagged"></span> marked <span class="k"></span> pending</p>

            <div class="controls">
              @if (s.attempt.mode === 'simulation' || !s.attempt.feedbackEnabled) {
                @if (!s.attempt.feedbackEnabled) {
                  @if (confirmFeedback()) {
                    <div class="confirm" role="alertdialog" aria-labelledby="cf-h" aria-describedby="cf-d">
                      <p id="cf-h"><strong>Show solutions for this attempt?</strong></p>
                      <p id="cf-d" class="small">The attempt becomes assisted permanently. Answers you already gave keep counting as first given.</p>
                      <button type="button" class="btn" (click)="enableFeedback()">Show solutions</button>
                      <button type="button" class="btn quiet" (click)="confirmFeedback.set(false)">Cancel</button>
                    </div>
                  } @else {
                    <button type="button" class="btn quiet small" (click)="confirmFeedback.set(true)">Turn on feedback…</button>
                  }
                }
              }
              @if (s.attempt.feedbackEnabled) {
                <button type="button" class="btn quiet small" (click)="disableFeedback()">Hide feedback{{ s.attempt.classification === 'assisted' ? ' (attempt stays assisted)' : '' }}</button>
              }
              @if (confirmFinish()) {
                <div class="confirm" role="alertdialog" aria-labelledby="fin-h">
                  <p id="fin-h"><strong>Finish this attempt?</strong></p>
                  <p class="small">{{ s.items.length - answeredCount() }} unanswered, {{ flaggedCount() }} marked for review. You cannot change answers afterwards.</p>
                  <button type="button" class="btn" (click)="finish()" [disabled]="busy()">Finish and see result</button>
                  <button type="button" class="btn quiet" (click)="confirmFinish.set(false)">Keep working</button>
                </div>
              } @else {
                <button type="button" class="btn" (click)="confirmFinish.set(true)">Finish attempt</button>
              }
            </div>
          </aside>
        </div>
      } @else if (!fatal()) { <p aria-live="polite">Loading attempt…</p> }
    </div>
  `,
  styleUrl: './runner.css',
})
export class RunnerPage implements OnInit, OnDestroy {
  id = input.required<string>();
  private api = inject(Api);
  private router = inject(Router);
  private stemEl = viewChild<ElementRef<HTMLElement>>('stem');

  protected state = signal<AttemptState | null>(null);
  protected index = signal(0);
  protected fatal = signal<string | null>(null);
  protected busy = signal(false);
  protected saveState = signal<SaveState>('idle');
  protected offline = signal(false);
  protected announce = signal('');
  protected confirmFinish = signal(false);
  protected confirmFeedback = signal(false);
  protected checkOnSelect = signal(false);
  private deadlineMs: number | null = null;
  protected remaining = signal<number | null>(null);
  private tick?: ReturnType<typeof setInterval>;
  private retry?: ReturnType<typeof setTimeout>;
  private pending = new Map<number, string[]>();
  private saving = false;
  private finishing = false;

  protected current = computed(() => this.state()?.items[this.index()] ?? null);
  protected closed = computed(() => this.state()?.attempt.status !== 'in_progress');
  protected answeredCount = computed(() => this.state()?.items.filter((i) => this.selectionOf(i).length > 0).length ?? 0);
  protected flaggedCount = computed(() => this.state()?.items.filter((i) => i.flagged).length ?? 0);
  protected modeName = computed(() => ({ practice: 'Practice', simulation: 'Simulation', custom: 'Custom set' })[this.state()?.attempt.mode ?? 'practice'] ?? '');
  protected timeText = computed(() => {
    const r = this.remaining() ?? 0;
    const h = Math.floor(r / 3600), m = Math.floor((r % 3600) / 60), s = r % 60;
    return (h > 0 ? h + ':' : '') + String(m).padStart(2, '0') + ':' + String(s).padStart(2, '0');
  });
  protected saveLabel = computed(() => ({ idle: '', saving: 'Saving…', saved: 'Answer saved', error: 'Not saved yet, retrying' })[this.saveState()]);

  constructor() {
    effect(() => { this.index(); queueMicrotask(() => this.stemEl()?.nativeElement.focus({ preventScroll: false })); });
  }

  async ngOnInit() {
    await this.load(true);
    this.tick = setInterval(() => this.onTick(), 1000);
  }

  ngOnDestroy() { clearInterval(this.tick); clearTimeout(this.retry); }

  /** Another tab or a reconnect may have changed the attempt: refresh from the server, which owns the state. */
  @HostListener('window:focus') onFocus() { if (this.state() && !this.finishing) this.load(false); }
  @HostListener('window:online') onOnline() { this.flush(); }

  private async load(first: boolean) {
    try {
      const s = await this.api.state(this.id());
      if (s.attempt.status !== 'in_progress') { this.router.navigate(['/attempts', this.id(), 'result'], { replaceUrl: true }); return; }
      // keep unsent local selections
      for (const [pos, sel] of this.pending) { const it = s.items.find((i) => i.position === pos); if (it) it.selected = sel; }
      this.state.set(s);
      this.deadlineMs = s.remainingSeconds === null ? null : performance.now() + s.remainingSeconds * 1000;
      this.onTick();
      if (first) {
        const firstOpen = s.items.findIndex((i) => i.selected.length === 0);
        this.index.set(firstOpen < 0 ? 0 : firstOpen);
      }
    } catch (e) {
      const a = toApiError(e);
      if (first) this.fatal.set(a.status === 404 ? 'This attempt does not exist or belongs to another account.' : a.message);
    }
  }

  private onTick() {
    if (this.deadlineMs === null) { this.remaining.set(null); return; }
    const r = Math.max(0, Math.round((this.deadlineMs - performance.now()) / 1000));
    this.remaining.set(r);
    if (r === 300) this.announce.set('Five minutes left.');
    if (r === 0 && !this.finishing) this.finish(true);
  }

  selectionOf(q: AttemptItem) { return this.pending.get(q.position) ?? q.selected; }
  isSelected(q: AttemptItem, id: string) { return this.selectionOf(q).includes(id); }
  wasScored(q: AttemptItem, id: string) { return q.solution?.scoredOptionIds.includes(id) ?? false; }
  atLimit(q: AttemptItem) { return q.questionType === 'multiple_response' && this.selectionOf(q).length >= q.selectCount; }
  canCheck(q: AttemptItem) { return this.selectionOf(q).length === q.selectCount; }
  correctLabels(q: AttemptItem) { return q.options.filter((o) => o.isCorrect).map((o) => o.label).join(' and '); }
  mapState(it: AttemptItem) { return [this.selectionOf(it).length ? 'answered' : 'not answered', it.flagged ? 'marked for review' : ''].filter(Boolean).join(', '); }

  go(i: number) {
    const n = this.state()?.items.length ?? 0;
    if (i >= 0 && i < n) { this.index.set(i); this.confirmFinish.set(false); }
  }

  choose(q: AttemptItem, optionId: string, checked: boolean) {
    let sel = [...this.selectionOf(q)];
    if (q.questionType === 'single_choice') sel = checked ? [optionId] : [];
    else sel = checked ? [...new Set([...sel, optionId])] : sel.filter((x) => x !== optionId);
    this.pending.set(q.position, sel);
    this.patchItem(q.position, { selected: sel });
    this.flush().then(() => {
      if (q.questionType === 'single_choice' && this.checkOnSelect() && this.state()?.attempt.feedbackEnabled && !q.feedbackRevealed && sel.length === 1)
        this.check(this.state()!.items.find((x) => x.position === q.position)!);
    });
  }

  private patchItem(pos: number, patch: Partial<AttemptItem>) {
    this.state.update((s) => s && { ...s, items: s.items.map((i) => (i.position === pos ? { ...i, ...patch } : i)) });
  }

  /** Sends queued selections in order; retries on network errors without losing the latest choice. */
  private async flush() {
    if (this.saving) return;
    this.saving = true;
    try {
      while (this.pending.size > 0) {
        const [pos, sel] = this.pending.entries().next().value!;
        this.saveState.set('saving');
        try {
          await this.api.answer(this.id(), pos, sel);
          if (this.pending.get(pos) === sel) this.pending.delete(pos);
          this.offline.set(false);
          this.saveState.set('saved');
        } catch (e) {
          const a = toApiError(e);
          if (a.status === 0 || a.status >= 500) {
            this.offline.set(true); this.saveState.set('error');
            clearTimeout(this.retry);
            this.retry = setTimeout(() => this.flush(), 3000);
            return;
          }
          this.pending.delete(pos);
          if (a.error === 'attempt_expired' || a.error === 'attempt_closed') { this.goResult(); return; }
          this.saveState.set('error');
          this.announce.set(a.message);
          await this.load(false);
        }
      }
    } finally { this.saving = false; }
  }

  async check(q: AttemptItem, revealWithoutAnswer = false) {
    this.busy.set(true);
    try {
      await this.flush();
      const updated = await this.api.check(this.id(), q.position, revealWithoutAnswer);
      this.patchItem(q.position, updated);
      this.state.update((s) => s && { ...s, attempt: { ...s.attempt, classification: 'assisted' } });
    } catch (e) { this.handle(e); }
    finally { this.busy.set(false); }
  }

  async toggleFlag(q: AttemptItem) {
    const flagged = !q.flagged;
    this.patchItem(q.position, { flagged });
    try { await this.api.flag(this.id(), q.position, flagged); }
    catch (e) { this.patchItem(q.position, { flagged: !flagged }); this.handle(e); }
  }

  async enableFeedback() {
    try {
      const r = await this.api.setFeedback(this.id(), true);
      this.state.update((s) => s && { ...s, attempt: { ...s.attempt, feedbackEnabled: r.feedbackEnabled, classification: r.classification as 'assisted' } });
      this.confirmFeedback.set(false);
      this.announce.set('Feedback is on. This attempt is now assisted.');
    } catch (e) { this.handle(e); }
  }

  async disableFeedback() {
    try {
      const r = await this.api.setFeedback(this.id(), false);
      this.state.update((s) => s && { ...s, attempt: { ...s.attempt, feedbackEnabled: r.feedbackEnabled, classification: r.classification as 'assisted' } });
    } catch (e) { this.handle(e); }
  }

  async finish(auto = false) {
    if (this.finishing) return;
    this.finishing = true; this.busy.set(true);
    try {
      if (!auto) await this.flush();
      await this.api.finish(this.id());
      this.goResult();
    } catch (e) {
      const a = toApiError(e);
      this.finishing = false;
      if (a.status === 0) { this.offline.set(true); setTimeout(() => this.finish(auto), 3000); }
      else this.handle(e);
    } finally { this.busy.set(false); }
  }

  private goResult() { this.router.navigate(['/attempts', this.id(), 'result'], { replaceUrl: true }); }

  private handle(e: unknown) {
    const a = toApiError(e);
    if (a.error === 'attempt_expired' || a.error === 'attempt_closed') { this.goResult(); return; }
    if (a.status === 0) this.offline.set(true);
    this.announce.set(a.message);
    this.fatal.set(null);
  }
}
