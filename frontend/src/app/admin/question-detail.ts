import { DatePipe } from '@angular/common';
import { Component, inject, input, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Api, toApiError } from '../core/api';

interface Opt { id: string; text: string; rationale: string; }
interface Version {
  questionId: string; externalId: string; familyId: string; versionId: string; version: number; status: string; statusNote: string | null;
  domainCode: string; objective: string; locale: string; scenarioId: string | null; questionType: string; selectCount: number; stem: string;
  options: Opt[]; correctOptionIds: string[]; explanation: string; difficulty: string; difficultyBasis: string; sourceIds: string[];
  contentHash: string; provenance: string; nextStates: string[];
}
interface Detail {
  certificationCode: string; latest: Version; scenario: { id: string; title: string; text: string } | null;
  validation: { severity: string; code: string; message: string }[];
  history: { id: string; versionNo: number; status: string; contentHash: string; createdAt: string; statusNote: string | null }[];
}

@Component({
  selector: 'app-question-detail',
  imports: [RouterLink, DatePipe],
  template: `
    <div class="page">
      <p class="small"><a routerLink="/admin">Administration</a> / question</p>
      @if (error()) { <div class="notice error" role="alert">{{ error() }}</div> }
      @if (message()) { <div class="notice ok" role="status">{{ message() }}</div> }
      @if (d(); as q) {
        <h1>{{ q.latest.externalId }} <span class="muted">v{{ q.latest.version }}</span></h1>
        <p><span class="tag">{{ label(q.latest.status) }}</span> {{ q.certificationCode }}, {{ q.latest.domainCode }}: {{ q.latest.objective }},
          {{ q.latest.questionType }}, locale {{ q.latest.locale }}, difficulty {{ q.latest.difficulty }} ({{ q.latest.difficultyBasis }})</p>

        @if (q.validation.length) {
          <div class="notice" [class.error]="hasErrors(q)" [class.warn]="!hasErrors(q)">
            <strong>Validation</strong>
            <ul class="small">@for (v of q.validation; track v.code + v.message) { <li>{{ v.severity }}: {{ v.message }}</li> }</ul>
          </div>
        }

        <section class="sheet">
          @if (q.scenario) { <p class="small"><strong>{{ q.scenario.title }}.</strong> {{ q.scenario.text }}</p> }
          <p class="stem">{{ q.latest.stem }}</p>
          <ol class="opts" type="A">
            @for (o of q.latest.options; track o.id) {
              <li [class.key]="q.latest.correctOptionIds.includes(o.id)">
                <strong>{{ o.id }}</strong> {{ o.text }} @if (q.latest.correctOptionIds.includes(o.id)) { <span class="tag ok">key</span> }
                <div class="small muted">{{ o.rationale }}</div>
              </li>
            }
          </ol>
          <p><strong>Explanation.</strong> {{ q.latest.explanation }}</p>
          <p class="small">Sources: {{ q.latest.sourceIds.join(', ') }}. Provenance: {{ q.latest.provenance }}.</p>
        </section>

        <section>
          <h2>Review workflow</h2>
          <p class="small muted">Publishing requires no validation errors, reachable registered sources and a verified exam language. Advancing does not skip any check.</p>
          <label class="field">Note (required for quarantine or retirement)
            <input type="text" [value]="note()" (input)="note.set($any($event.target).value)" />
          </label>
          <p class="btns">
            @for (s of q.latest.nextStates; track s) {
              <button type="button" class="btn" [class.secondary]="s === 'draft' || s === 'retired' || s === 'quarantined'" (click)="transition(s)" [disabled]="busy()">Move to {{ label(s) }}</button>
            } @empty { <span class="muted">No further transitions.</span> }
          </p>
        </section>

        <section>
          <h2>Edit as new version</h2>
          <p class="small muted">Edits create a new draft version. Attempts already taken keep the version they used.</p>
          @if (!edit()) { <button type="button" class="btn secondary" (click)="startEdit(q.latest)">Edit…</button> }
          @if (edit(); as e) {
            <div class="form">
              <label class="field">Stem <textarea rows="4" [value]="e.stem" (input)="e.stem = $any($event.target).value"></textarea></label>
              @for (o of e.options; track o.id) {
                <fieldset class="optedit">
                  <legend>Option {{ o.id }}</legend>
                  <label class="check"><input type="checkbox" [checked]="e.correctOptionIds.includes(o.id)" (change)="toggleKey(o.id)" /> Correct</label>
                  <label class="field">Text <input type="text" [value]="o.text" (input)="o.text = $any($event.target).value" /></label>
                  <label class="field">Why <input type="text" [value]="o.rationale" (input)="o.rationale = $any($event.target).value" /></label>
                </fieldset>
              }
              <label class="field">Explanation <textarea rows="3" [value]="e.explanation" (input)="e.explanation = $any($event.target).value"></textarea></label>
              <label class="field">Sources (comma separated ids) <input type="text" [value]="e.sourceIds.join(', ')" (input)="e.sourceIds = splitIds($any($event.target).value)" /></label>
              <label class="field">Change note <input type="text" [value]="e.note" (input)="e.note = $any($event.target).value" /></label>
              <p class="btns"><button type="button" class="btn" (click)="saveVersion()">Save new version</button>
                <button type="button" class="btn quiet" (click)="edit.set(null)">Cancel</button></p>
            </div>
          }
        </section>

        <h2>History</h2>
        <table class="data">
          <thead><tr><th scope="col">Version</th><th scope="col">Status</th><th scope="col">Created</th><th scope="col">Note</th></tr></thead>
          <tbody>@for (h of q.history; track h.id) { <tr><td>v{{ h.versionNo }}</td><td>{{ label(h.status) }}</td><td>{{ h.createdAt | date: 'short' }}</td><td class="small">{{ h.statusNote }}</td></tr> }</tbody>
        </table>
      }
    </div>
  `,
  styles: `
    .stem { white-space: pre-line; font-size: var(--step-1); }
    .opts li { margin: 0.4rem 0; padding: 0.4rem 0.6rem; border-left: 3px solid transparent; }
    .opts li.key { border-left-color: var(--correct); background: var(--correct-soft); }
    .btns { display: flex; gap: 0.5rem; flex-wrap: wrap; margin-top: 0.75rem; }
    .form { display: grid; gap: 0.75rem; }
    .optedit { border: 1px solid var(--rule); border-radius: var(--radius-s); display: grid; gap: 0.4rem; }
    .check { display: flex; gap: 0.4rem; align-items: center; }
    section { margin-top: 2rem; }
  `,
})
export class QuestionDetailPage implements OnInit {
  id = input.required<string>();
  private api = inject(Api);
  protected d = signal<Detail | null>(null);
  protected error = signal<string | null>(null);
  protected message = signal<string | null>(null);
  protected note = signal('');
  protected busy = signal(false);
  protected edit = signal<(Version & { note: string }) | null>(null);

  async ngOnInit() { await this.load(); }
  async load() {
    try { this.d.set(await this.api.get<Detail>(`/api/admin/questions/${this.id()}`)); } catch (e) { this.error.set(toApiError(e).message); }
  }
  label(s: string) { return s.replaceAll('_', ' '); }
  hasErrors(q: Detail) { return q.validation.some((v) => v.severity === 'error'); }
  splitIds(v: string) { return v.split(',').map((x) => x.trim()).filter(Boolean); }

  async transition(to: string) {
    this.busy.set(true); this.error.set(null); this.message.set(null);
    try {
      await this.api.post(`/api/admin/questions/${this.id()}/transition`, { to, note: this.note() || null });
      this.message.set(`Moved to ${this.label(to)}.`); this.note.set('');
      await this.load();
    } catch (e) {
      const a = toApiError(e);
      const details = Array.isArray(a.details) ? ' ' + (a.details as any[]).map((x) => (typeof x === 'string' ? x : x.message)).join(' ') : '';
      this.error.set(a.message + details);
    } finally { this.busy.set(false); }
  }

  startEdit(v: Version) { this.edit.set({ ...structuredClone(v), note: '' }); }
  toggleKey(id: string) {
    const e = this.edit(); if (!e) return;
    e.correctOptionIds = e.correctOptionIds.includes(id) ? e.correctOptionIds.filter((x) => x !== id) : [...e.correctOptionIds, id].sort();
    this.edit.set({ ...e });
  }
  async saveVersion() {
    const e = this.edit(); if (!e) return;
    try {
      await this.api.post(`/api/admin/questions/${this.id()}/versions`, {
        domainCode: e.domainCode, objective: e.objective, scenarioId: e.scenarioId, questionType: e.questionType,
        selectCount: e.correctOptionIds.length, stem: e.stem, options: e.options, correctOptionIds: e.correctOptionIds,
        explanation: e.explanation, difficulty: e.difficulty, sourceIds: e.sourceIds, note: e.note || null,
      });
      this.edit.set(null); this.message.set('New draft version saved.'); await this.load();
    } catch (err) {
      const a = toApiError(err);
      const details = Array.isArray(a.details) ? ' ' + (a.details as any[]).map((x) => x.message).join(' ') : '';
      this.error.set(a.message + details);
    }
  }
}
