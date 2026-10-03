import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, inject, input, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Api, AttemptItem, AttemptResult, toApiError } from '../core/api';

@Component({
  selector: 'app-result',
  imports: [RouterLink, DecimalPipe, DatePipe],
  template: `
    <div class="page">
      @if (error()) { <div class="notice error" role="alert">{{ error() }}</div> }
      @if (r(); as res) {
        @if (res.attempt.score; as sc) {
          <p class="crumb small"><a routerLink="/my">My exams</a> / {{ res.attempt.certificationCode }}</p>
          <div class="headline">
            <div>
              <h1>{{ sc.passed ? 'Above the practice pass mark' : 'Below the practice pass mark' }}</h1>
              <p class="muted">{{ modeName(res.attempt.mode) }}, {{ res.attempt.status === 'expired' ? 'time ran out' : 'submitted' }}
                {{ res.attempt.finishedAt | date: 'medium' }}. Profile version {{ res.attempt.profileVersion }}.</p>
              <p>
                @if (res.attempt.classification === 'assisted') { <span class="tag warn">Assisted: solutions were shown</span> }
                @else { <span class="tag ok">Clean run</span> }
                @if (res.attempt.preview) { <span class="tag warn">Admin preview, not counted in progress</span> }
              </p>
            </div>
            <div class="score" [class.pass]="sc.passed">
              <span class="pct">{{ sc.percent | number: '1.0-1' }}%</span>
              <span class="small">{{ sc.pointsEarned }} of {{ sc.pointsMax }} points, pass mark {{ sc.passPercent | number: '1.0-0' }}%</span>
            </div>
          </div>
          <dl class="nums">
            <div><dt>Correct</dt><dd>{{ sc.correct }}</dd></div>
            <div><dt>Incorrect</dt><dd>{{ sc.incorrect }}</dd></div>
            <div><dt>Not answered</dt><dd>{{ sc.omitted }}</dd></div>
            <div><dt>Time used</dt><dd>{{ minutes(sc.timeUsedSeconds) }}</dd></div>
          </dl>
          <p class="small muted">{{ res.disclaimer }}</p>

          <h2>By domain</h2>
          <div class="table-scroll">
            <table class="data">
              <thead><tr><th scope="col">Domain</th><th scope="col">Correct</th><th scope="col">Share</th></tr></thead>
              <tbody>
                @for (d of res.byDomain; track d.code) {
                  <tr [class.weak]="res.toReinforce.includes(d.code)">
                    <th scope="row">{{ d.name }}</th>
                    <td>{{ d.correct }} / {{ d.total }}</td>
                    <td><span class="meter"><span [style.width.%]="100 * d.correct / d.total"></span></span></td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
          @if (res.byDomain.length && sc.pointsMax < 20) { <p class="small muted">Small sample: a few questions per domain say little about readiness.</p> }

          <h2>Review</h2>
          <div class="filters" role="group" aria-label="Filter review">
            <button type="button" class="btn quiet small" [attr.aria-pressed]="filter() === 'all'" (click)="filter.set('all')">All</button>
            <button type="button" class="btn quiet small" [attr.aria-pressed]="filter() === 'wrong'" (click)="filter.set('wrong')">Incorrect and unanswered</button>
          </div>
          @for (q of visible(res.review); track q.position) {
            <article class="rev" [class.ok]="q.solution?.isCorrect">
              @if (q.scenario) { <p class="small muted"><strong>{{ q.scenario.title }}.</strong> {{ q.scenario.text }}</p> }
              <h3>Question {{ q.position }} <span class="small muted">{{ q.domainCode }}</span>
                <span class="tag" [class.ok]="q.solution?.isCorrect" [class.bad]="q.solution?.isCorrect === false">{{ verdict(q) }}</span></h3>
              <p class="stem">{{ q.stem }}</p>
              <ul class="ropts">
                @for (o of q.options; track o.id) {
                  <li [class.right]="o.isCorrect" [class.mine]="q.solution?.scoredOptionIds?.includes(o.id)">
                    <strong>{{ o.label }}.</strong> {{ o.text }}
                    @if (q.solution?.scoredOptionIds?.includes(o.id)) { <em class="small"> (your answer)</em> }
                    <span class="why small">{{ o.rationale }}</span>
                  </li>
                }
              </ul>
              <p>{{ q.solution?.explanation }}</p>
              @if (q.solution?.sources?.length) {
                <p class="small">Study: @for (s of q.solution!.sources; track s.id; let last = $last) { <a [href]="s.url" target="_blank" rel="noopener noreferrer">{{ s.title }}</a>{{ last ? '' : ', ' }} }</p>
              }
            </article>
          }
          <p><a class="btn" [routerLink]="['/exams', res.attempt.certificationCode]">Start another attempt</a>
             <a class="btn secondary" [routerLink]="['/progress', res.attempt.certificationCode]">See progress</a></p>
        }
      } @else if (!error()) { <p aria-live="polite">Loading result…</p> }
    </div>
  `,
  styles: `
    .headline { display: flex; justify-content: space-between; gap: 2rem; flex-wrap: wrap; align-items: flex-start; }
    .score { border: 3px solid var(--ink); border-radius: var(--radius-m); padding: 0.75rem 1.25rem; display: grid; text-align: right; background: var(--sheet); }
    .score.pass { border-color: var(--correct); }
    .pct { font-size: var(--step-5); font-weight: 800; line-height: 1; font-variant-numeric: tabular-nums; }
    .nums { display: flex; flex-wrap: wrap; gap: 1rem 2.5rem; margin: 1rem 0; }
    .nums dt { color: var(--ink-soft); font-size: var(--step--1); } .nums dd { margin: 0; font-weight: 800; font-size: var(--step-2); }
    .meter { display: inline-block; width: 160px; height: 10px; background: var(--rule); border-radius: 2px; }
    .meter span { display: block; height: 100%; background: var(--ink); border-radius: 2px; }
    tr.weak th { box-shadow: inset 4px 0 0 var(--highlight); }
    .filters { display: flex; gap: 0.5rem; margin-bottom: 1rem; }
    .filters [aria-pressed='true'] { background: var(--ink); color: #fff; }
    .rev { background: var(--sheet); border: 1px solid var(--rule); border-left: 4px solid var(--wrong); border-radius: var(--radius-s); padding: 1rem 1.25rem; margin-bottom: 1rem; }
    .rev.ok { border-left-color: var(--correct); }
    .rev h3 { display: flex; gap: 0.75rem; align-items: baseline; flex-wrap: wrap; }
    .stem { white-space: pre-line; }
    .ropts { list-style: none; padding: 0; display: grid; gap: 0.4rem; }
    .ropts li { padding: 0.5rem 0.7rem; border: 1px solid var(--rule); border-radius: var(--radius-s); }
    .ropts li.right { border-color: var(--correct); background: var(--correct-soft); }
    .ropts li.mine:not(.right) { border-color: var(--wrong); background: var(--wrong-soft); }
    .why { display: block; color: var(--ink-soft); margin-top: 0.2rem; }
  `,
})
export class ResultPage implements OnInit {
  id = input.required<string>();
  private api = inject(Api);
  protected r = signal<AttemptResult | null>(null);
  protected error = signal<string | null>(null);
  protected filter = signal<'all' | 'wrong'>('all');

  async ngOnInit() {
    try { this.r.set(await this.api.result(this.id())); }
    catch (e) { this.error.set(toApiError(e).message); }
  }
  visible(items: AttemptItem[]) { return this.filter() === 'all' ? items : items.filter((i) => i.solution?.isCorrect !== true); }
  verdict(q: AttemptItem) { return q.solution?.isCorrect === true ? 'Correct' : q.solution?.isCorrect === false ? 'Incorrect' : 'Not answered'; }
  minutes(s: number) { const m = Math.floor(s / 60); return `${m} min ${s % 60} s`; }
  modeName(m: string) { return m === 'simulation' ? 'Simulation' : m === 'custom' ? 'Custom set' : 'Practice'; }
}
