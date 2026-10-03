import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, inject, input, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Api, Progress, ProgressDomain, toApiError } from '../core/api';

@Component({
  selector: 'app-progress',
  imports: [RouterLink, DatePipe, DecimalPipe],
  template: `
    <div class="page">
      <p class="crumb small"><a routerLink="/my">My exams</a> / {{ code() }}</p>
      <h1>{{ code() }} progress</h1>
      @if (error()) { <div class="notice error" role="alert">{{ error() }}</div> }
      @if (p(); as pr) {
        <p class="muted">{{ pr.note }}</p>
        <div class="two">
          @for (g of groups(pr); track g.title) {
            <section class="sheet" [attr.aria-labelledby]="'g-' + g.key">
              <h2 [id]="'g-' + g.key">{{ g.title }} <span class="small muted">{{ g.attempts }} attempts</span></h2>
              <table class="data">
                <thead><tr><th scope="col">Domain</th><th scope="col">Correct / answered</th></tr></thead>
                <tbody>
                  @for (d of g.rows; track d.code) {
                    <tr><th scope="row">{{ d.name }}</th>
                      <td>{{ d.correct }} / {{ d.answered }} @if (d.answered) { <span class="muted">({{ 100 * d.correct / d.answered | number: '1.0-0' }}%)</span> }
                        @if (d.sampleNote && d.answered) { <br /><span class="small muted">{{ d.sampleNote }}</span> }</td></tr>
                  }
                </tbody>
              </table>
            </section>
          }
        </div>
        <h2>Timeline</h2>
        @if (!pr.timeline.length) { <p>No finished attempts for this exam yet.</p> }
        @else {
          <ol class="timeline">
            @for (t of pr.timeline; track t.id) {
              <li>
                <span class="bar" [class.assisted]="t.classification === 'assisted'"><span [style.width.%]="t.percent"></span></span>
                <a [routerLink]="['/attempts', t.id, 'result']">{{ t.finishedAt | date: 'mediumDate' }}</a>
                {{ t.mode }}, {{ t.classification }}: {{ t.pointsEarned }}/{{ t.pointsMax }} ({{ t.percent | number: '1.0-0' }}%), profile v{{ t.profileVersion }}
              </li>
            }
          </ol>
        }
      }
    </div>
  `,
  styles: `
    .two { display: grid; grid-template-columns: 1fr 1fr; gap: 1.25rem; }
    .timeline { padding-left: 1.2rem; display: grid; gap: 0.4rem; }
    .bar { display: inline-block; width: 120px; height: 10px; background: var(--rule); vertical-align: middle; margin-right: 0.5rem; }
    .bar span { display: block; height: 100%; background: var(--ink); }
    .bar.assisted span { background: repeating-linear-gradient(45deg, var(--ink-soft) 0 4px, var(--rule-strong) 4px 8px); }
    @media (max-width: 820px) { .two { grid-template-columns: 1fr; } }
  `,
})
export class ProgressPage implements OnInit {
  code = input.required<string>();
  private api = inject(Api);
  protected p = signal<Progress | null>(null);
  protected error = signal<string | null>(null);
  async ngOnInit() {
    try { this.p.set(await this.api.progress(this.code())); } catch (e) { this.error.set(toApiError(e).message); }
  }
  groups(pr: Progress): { key: string; title: string; attempts: number; rows: ProgressDomain[] }[] {
    return [
      { key: 'clean', title: 'Clean attempts', attempts: pr.clean.attempts, rows: pr.clean.byDomain },
      { key: 'assisted', title: 'Assisted attempts', attempts: pr.assisted.attempts, rows: pr.assisted.byDomain },
    ];
  }
}
