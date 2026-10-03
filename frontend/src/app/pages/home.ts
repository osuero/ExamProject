import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SlicePipe } from '@angular/common';
import { Api, AttemptSummary, CatalogItem, toApiError } from '../core/api';
import { Auth } from '../core/auth';
import { ExamCard } from '../shared/exam-card';

@Component({
  selector: 'app-home',
  imports: [RouterLink, ExamCard, SlicePipe],
  template: `
    <section class="hero">
      <div class="page hero-inner">
        <div class="copy">
          <h1>Practise for the Claude Foundations exams on realistic scenarios.</h1>
          <p class="lede">Original questions mapped to the official exam guides for Developer and Architect.
            Practise with explanations for every option, or sit a timed simulation with the answers hidden until you finish.</p>
          <div class="cta">
            <a class="btn" routerLink="/exams">Browse exams</a>
            @if (!auth.me().authenticated) { <a class="btn secondary" routerLink="/login">Sign in with email</a> }
          </div>
        </div>
        <div class="sheets" aria-hidden="true">
          @for (e of exams(); track e.code) {
            <div class="sheet-art">
              <span class="sheet-label">{{ e.code }}: {{ e.questionCount }} items</span>
              <div class="bubbles">
                @for (b of range(e.questionCount); track b) { <i [class.f]="b % 7 === 2 || b % 11 === 5" [class.h]="b === 17 || b === 41"></i> }
              </div>
            </div>
          }
        </div>
      </div>
    </section>

    <div class="page">
      @if (inProgress().length > 0) {
        <section class="resume sheet" aria-labelledby="resume-h">
          <h2 id="resume-h">Continue where you left off</h2>
          <ul>
            @for (a of inProgress(); track a.id) {
              <li>
                <a [routerLink]="['/attempts', a.id]">{{ a.certificationCode }} {{ modeName(a.mode) }}</a>
                <span class="muted small">started {{ a.startedAt | slice: 0 : 10 }}{{ a.deadlineAt ? ', timed' : '' }}</span>
              </li>
            }
          </ul>
        </section>
      }

      <h2 class="section-h">Exams</h2>
      @if (error()) { <div class="notice error" role="alert">{{ error() }}</div> }
      <div class="grid">
        @for (e of exams(); track e.code) { <app-exam-card [exam]="e" /> }
      </div>

      <section class="how" aria-labelledby="how-h">
        <h2 id="how-h">How practice works here</h2>
        <div class="cols">
          <div><h3>Practice</h3><p>Check each answer when you are ready. You see why the right option fits and why each other option does not, with links to the documentation.</p></div>
          <div><h3>Simulation</h3><p>Same question count, time and domain mix as the official profile. The server keeps the clock, so reloading or losing the connection does not pause it.</p></div>
          <div><h3>Honest scores</h3><p>One point per question, exact set for multiple response. Attempts where you viewed solutions are labelled assisted and kept apart from clean runs.</p></div>
        </div>
      </section>
    </div>
  `,
  styles: `
    .hero { background: var(--sheet); border-bottom: 1px solid var(--rule); }
    .hero-inner { display: grid; grid-template-columns: 1.2fr 1fr; gap: 2.5rem; align-items: center; padding-block: 3rem; }
    h1 { font-size: var(--step-5); letter-spacing: -0.025em; max-width: 16ch; }
    .lede { font-size: var(--step-1); color: var(--ink-soft); max-width: 46ch; }
    .cta { display: flex; gap: 0.75rem; flex-wrap: wrap; }
    .sheets { display: grid; gap: 1.25rem; }
    .sheet-art { border: 2px solid var(--ink); border-radius: var(--radius-m); padding: 0.75rem; background: var(--paper); }
    .sheet-label { font-weight: 600; font-size: var(--step--1); display: block; margin-bottom: 0.5rem; }
    .bubbles { display: grid; grid-template-columns: repeat(15, 1fr); gap: 5px; }
    .bubbles i { aspect-ratio: 1; border-radius: 50%; border: 1.5px solid var(--rule-strong); }
    .bubbles i.f { background: var(--ink); border-color: var(--ink); }
    .bubbles i.h { background: var(--highlight); border-color: #b88400; }
    .section-h { margin-top: 2rem; }
    .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); gap: 1.25rem; }
    .resume { margin-top: 2rem; }
    .resume ul { margin: 0; padding-left: 1.2rem; }
    .resume li { margin: 0.3rem 0; }
    .how { margin-top: 3rem; }
    .cols { display: grid; grid-template-columns: repeat(3, 1fr); gap: 1.5rem; }
    .cols h3 { margin-bottom: 0.3rem; }
    @media (max-width: 820px) {
      .hero-inner { grid-template-columns: 1fr; padding-block: 2rem; }
      h1 { font-size: var(--step-4); }
      .cols { grid-template-columns: 1fr; }
    }
  `,
})
export class HomePage implements OnInit {
  private api = inject(Api);
  protected auth = inject(Auth);
  protected exams = signal<CatalogItem[]>([]);
  protected inProgress = signal<AttemptSummary[]>([]);
  protected error = signal<string | null>(null);
  range(n: number) { return Array.from({ length: n }, (_, i) => i); }
  modeName(m: string) { return m === 'simulation' ? 'simulation' : m === 'custom' ? 'custom set' : 'practice'; }

  async ngOnInit() {
    try { this.exams.set(await this.api.catalog()); } catch (e) { this.error.set(toApiError(e).message); }
    if (this.auth.me().authenticated) {
      try { this.inProgress.set((await this.api.attempts()).filter((a) => a.status === 'in_progress').slice(0, 5)); } catch { /* optional */ }
    }
  }
}
