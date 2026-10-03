import { Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Api, CatalogDetail, StartRequest, toApiError } from '../core/api';
import { Auth } from '../core/auth';
import { WeightBar } from '../shared/weight-bar';

type Mode = 'practice' | 'simulation' | 'custom';

@Component({
  selector: 'app-exam-detail',
  imports: [RouterLink, WeightBar],
  template: `
    <div class="page">
      @if (error()) { <div class="notice error" role="alert">{{ error() }}</div> }
      @if (exam(); as e) {
        <p class="crumb small"><a routerLink="/exams">Exams</a> / {{ e.code }}</p>
        <div class="top">
          <div>
            <h1>{{ e.title }}</h1>
            <p class="lede">{{ e.description }}</p>
            <ul class="facts">
              <li><strong>{{ e.profile.questionCount }}</strong> questions</li>
              <li><strong>{{ e.profile.examDurationMinutes }}</strong> minutes to answer</li>
              <li>Language: <strong>{{ e.languageStatus === 'verified' ? languages(e) : 'pending verification' }}</strong></li>
              <li>Level: <strong>{{ e.level }}</strong></li>
            </ul>
            <p class="small muted">{{ e.disclaimer }}</p>
          </div>

          <section class="start sheet" aria-labelledby="start-h">
            <h2 id="start-h">Start</h2>
            @if (!auth.me().authenticated) {
              <p>Sign in to save your answers and history.</p>
              <a class="btn" routerLink="/login" [queryParams]="{ next: '/exams/' + e.code }">Sign in to start</a>
            } @else {
              <fieldset class="modes">
                <legend>Mode</legend>
                @for (m of e.modes; track m.id) {
                  <label class="mode" [class.sel]="mode() === m.id">
                    <input type="radio" name="mode" [value]="m.id" [checked]="mode() === m.id" (change)="mode.set($any(m.id))" />
                    <span><strong>{{ m.name }}</strong><br /><span class="small muted">{{ m.description }}</span></span>
                  </label>
                }
              </fieldset>

              @if (mode() !== 'simulation') {
                <div class="opts">
                  <label class="field">Questions
                    <input type="number" min="1" [max]="maxCount()" [value]="count()" (input)="count.set(+$any($event.target).value)" />
                  </label>
                  <label class="field">Time limit in minutes (empty for untimed)
                    <input type="number" min="1" max="600" [value]="duration() ?? ''" (input)="setDuration($any($event.target).value)" />
                  </label>
                  <label class="check"><input type="checkbox" [checked]="feedback()" (change)="feedback.set($any($event.target).checked)" />
                    Show feedback while answering</label>
                  @if (mode() === 'custom' || mode() === 'practice') {
                    <fieldset class="doms">
                      <legend>Domains (none selected means all)</legend>
                      @for (d of e.domains; track d.code) {
                        <label class="check small"><input type="checkbox" [checked]="domains().includes(d.code)" (change)="toggleDomain(d.code)" /> {{ d.name }}</label>
                      }
                    </fieldset>
                  }
                </div>
              } @else {
                <p class="small">Feedback stays hidden until you finish. Turning it on during the attempt marks the attempt as assisted for good.</p>
              }

              @if (auth.isAdmin()) {
                <label class="check small admin"><input type="checkbox" [checked]="preview()" (change)="preview.set($any($event.target).checked)" />
                  Admin preview: include unpublished questions (not counted in progress)</label>
              }

              @if (!preview() && e.availability.publishedQuestions === 0) {
                <div class="notice warn small">No questions are published for this exam yet. The bank is in review.</div>
              } @else if (!preview() && mode() === 'simulation' && !e.availability.fullSimulationAvailable) {
                <div class="notice warn small">A full simulation needs {{ e.profile.questionCount }} published questions; {{ e.availability.publishedQuestions }} are available. Use practice or a custom set meanwhile.</div>
              }
              @if (startError()) { <div class="notice error small" role="alert">{{ startError() }}</div> }
              <button class="btn" type="button" (click)="start(e)" [disabled]="busy()">{{ busy() ? 'Preparing…' : 'Start ' + modeLabel() }}</button>
            }
          </section>
        </div>

        <section aria-labelledby="dom-h">
          <h2 id="dom-h">Syllabus</h2>
          <app-weight-bar [domains]="e.domains" />
          <div class="domain-list">
            @for (d of e.domains; track d.code) {
              <article class="dom">
                <header>
                  <h3>{{ d.name }}</h3>
                  <span class="weight">{{ d.weightPercent }}%</span>
                </header>
                <ul class="small">
                  @for (o of d.objectives; track o) { <li>{{ o }}</li> }
                </ul>
                <p class="small muted">{{ d.publishedQuestions }} published questions</p>
              </article>
            }
          </div>
        </section>

        <section class="about" aria-labelledby="about-h">
          <h2 id="about-h">About this profile</h2>
          <dl>
            <dt>Profile</dt><dd>Version {{ e.profile.version }}, {{ e.profile.blueprintVersion }} ({{ statusLabel(e.profile.verificationStatus) }})</dd>
            <dt>Question types</dt><dd>Single answer and multiple response; each question says how many options to select.</dd>
            <dt>Simulator scoring</dt><dd>One point per question, exact set for multiple response, no penalty. Pass mark here: {{ e.profile.simulatorPassPercent }}% of points. This is a rule of this simulator.</dd>
            <dt>Official scoring</dt><dd>{{ e.profile.officialScoreReference }}</dd>
            @if (e.profile.appointmentDurationMinutes) { <dt>Seat time</dt><dd>About {{ e.profile.appointmentDurationMinutes }} minutes including check-in. Accommodations are approved individually by Pearson VUE.</dd> }
            @if (e.profile.notes) { <dt>Notes</dt><dd>{{ e.profile.notes }}</dd> }
          </dl>
          <h3>Sources</h3>
          <ul class="small">
            @for (s of e.profile.sources; track s.id) { <li><a [href]="s.url" target="_blank" rel="noopener noreferrer">{{ s.title }}</a></li> }
          </ul>
        </section>
      } @else if (!error()) { <p aria-live="polite">Loading…</p> }
    </div>
  `,
  styles: `
    .crumb { margin-bottom: 0.5rem; }
    .top { display: grid; grid-template-columns: 1.4fr 1fr; gap: 2rem; align-items: start; margin-bottom: 2rem; }
    .lede { font-size: var(--step-1); color: var(--ink-soft); }
    .facts { list-style: none; padding: 0; display: flex; flex-wrap: wrap; gap: 0.5rem 1.5rem; margin: 0 0 1rem; }
    .start { position: sticky; top: 1rem; display: grid; gap: 0.75rem; }
    .start h2 { margin: 0; }
    fieldset { border: 0; padding: 0; margin: 0; }
    legend { font-weight: 600; margin-bottom: 0.4rem; }
    .modes { display: grid; gap: 0.5rem; }
    .mode { display: flex; gap: 0.6rem; align-items: flex-start; padding: 0.6rem; border: 2px solid var(--rule); border-radius: var(--radius-s); cursor: pointer; }
    .mode.sel { border-color: var(--action); background: var(--action-soft); }
    .mode input { margin-top: 0.3rem; }
    .opts { display: grid; gap: 0.75rem; }
    .check { display: flex; gap: 0.5rem; align-items: center; }
    .check input { width: 1.1rem; height: 1.1rem; }
    .doms { display: grid; gap: 0.25rem; }
    .admin { background: #fff8db; padding: 0.4rem; }
    .domain-list { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 1rem; margin-top: 1rem; }
    .dom { background: var(--sheet); border: 1px solid var(--rule); border-radius: var(--radius-m); padding: 1rem; }
    .dom header { display: flex; justify-content: space-between; gap: 1rem; align-items: baseline; border-bottom: 2px solid var(--ink); margin-bottom: 0.5rem; }
    .dom h3 { font-size: var(--step-0); margin: 0 0 0.3rem; }
    .weight { font-weight: 800; font-size: var(--step-1); font-variant-numeric: tabular-nums; }
    .dom ul { padding-left: 1.1rem; margin: 0 0 0.5rem; }
    .about { margin-top: 2.5rem; max-width: var(--measure); }
    dl { display: grid; grid-template-columns: 11rem 1fr; gap: 0.5rem 1rem; }
    dt { font-weight: 600; } dd { margin: 0; }
    @media (max-width: 820px) { .top { grid-template-columns: 1fr; } .start { position: static; } dl { grid-template-columns: 1fr; } dd { margin-bottom: 0.5rem; } }
  `,
})
export class ExamDetailPage implements OnInit {
  code = input.required<string>();
  private api = inject(Api);
  private router = inject(Router);
  protected auth = inject(Auth);
  protected exam = signal<CatalogDetail | null>(null);
  protected error = signal<string | null>(null);
  protected startError = signal<string | null>(null);
  protected mode = signal<Mode>('practice');
  protected count = signal(10);
  protected duration = signal<number | null>(null);
  protected feedback = signal(true);
  protected domains = signal<string[]>([]);
  protected preview = signal(false);
  protected busy = signal(false);
  protected maxCount = computed(() => 200);
  protected modeLabel = computed(() => ({ practice: 'practice', simulation: 'simulation', custom: 'custom set' })[this.mode()]);

  async ngOnInit() {
    try { this.exam.set(await this.api.catalogDetail(this.code())); }
    catch (e) { const a = toApiError(e); this.error.set(a.status === 404 ? 'This exam does not exist.' : a.message); }
  }

  languages(e: CatalogDetail) { return e.languages.map((l) => (l.locale === 'en' ? 'English' : l.locale)).join(', '); }
  statusLabel(s: string) { return s === 'confirmed_official_exam_guide' ? 'checked against the official exam guide' : s.replaceAll('_', ' '); }
  setDuration(v: string) { this.duration.set(v === '' ? null : Math.max(1, Math.min(600, +v))); }
  toggleDomain(c: string) { this.domains.update((d) => (d.includes(c) ? d.filter((x) => x !== c) : [...d, c])); }

  async start(e: CatalogDetail) {
    this.startError.set(null); this.busy.set(true);
    const req: StartRequest = { certificationCode: e.code, mode: this.mode(), preview: this.preview() || undefined };
    if (this.mode() !== 'simulation') {
      req.questionCount = this.count(); req.durationMinutes = this.duration(); req.feedback = this.feedback();
      if (this.domains().length) req.domainCodes = this.domains();
    }
    try {
      const r = await this.api.start(req);
      this.router.navigate(['/attempts', r.id]);
    } catch (err) {
      const a = toApiError(err);
      this.startError.set(a.message);
    } finally { this.busy.set(false); }
  }
}
