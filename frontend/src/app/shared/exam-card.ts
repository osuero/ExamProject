import { Component, computed, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CatalogItem } from '../core/api';
import { WeightBar } from './weight-bar';

@Component({
  selector: 'app-exam-card',
  imports: [RouterLink, WeightBar],
  template: `
    <article class="card" [class.open]="open()" (mouseenter)="onHover(true)" (mouseleave)="onHover(false)"
             (focusin)="focus.set(true)" (focusout)="onFocusOut($event)">
      <div class="head">
        <span class="code">{{ exam().code }}</span>
        <span class="level">{{ exam().level }}</span>
      </div>
      <h3><a [routerLink]="['/exams', exam().code]" class="title">{{ exam().title }}</a></h3>
      <p class="desc">{{ exam().shortDescription }}</p>
      <app-weight-bar [domains]="exam().domains" />
      <dl class="facts">
        <div><dt>Format</dt><dd>{{ exam().questionCount }} questions in {{ exam().durationMinutes }} minutes</dd></div>
        <div><dt>Language</dt><dd>{{ languageLabel() }}</dd></div>
        <div><dt>Available here</dt><dd>{{ availabilityLabel() }}</dd></div>
      </dl>
      <button type="button" class="peek btn quiet small" [attr.aria-expanded]="open()" [attr.aria-controls]="previewId()"
              (click)="toggled.set(!toggled())">
        {{ open() ? 'Hide quick look' : 'Quick look' }}
      </button>
      <div class="preview" [id]="previewId()" role="region" [attr.aria-label]="'Quick look: ' + exam().title" [hidden]="!open()">
        <h4>Domains and weights</h4>
        <ol class="domains">
          @for (d of exam().domains; track d.code) {
            <li><span>{{ d.name }}</span><span class="w">{{ d.weightPercent }}%</span></li>
          }
        </ol>
        <p class="small muted">Modes: practice with explanations, timed simulation, custom set.</p>
        <a class="btn" [routerLink]="['/exams', exam().code]">See syllabus and start</a>
      </div>
    </article>
  `,
  styles: `
    :host { display: block; }
    .card { position: relative; background: var(--sheet); border: 1px solid var(--rule); border-radius: var(--radius-m);
      padding: 1.25rem 1.25rem 1rem; height: 100%; display: flex; flex-direction: column; gap: 0.5rem; }
    .card:hover, .card:focus-within { border-color: var(--ink); }
    .head { display: flex; justify-content: space-between; font-weight: 600; font-size: var(--step--1); color: var(--ink-soft); }
    .code { color: var(--ink); background: var(--highlight); padding: 0 0.35rem; border-radius: 2px; }
    h3 { margin: 0.25rem 0 0; font-size: var(--step-2); }
    .title { color: var(--ink); text-decoration: none; }
    .title:hover { text-decoration: underline; }
    .title::after { content: ''; position: absolute; inset: 0; z-index: 0; }
    .desc { color: var(--ink-soft); margin-bottom: 0.25rem; }
    .facts { display: grid; gap: 0.2rem; margin: 0.5rem 0 0; font-size: var(--step--1); }
    .facts div { display: flex; gap: 0.5rem; }
    .facts dt { color: var(--ink-soft); min-width: 6.5rem; }
    .facts dd { margin: 0; font-weight: 600; }
    .peek { align-self: flex-start; position: relative; z-index: 1; margin-top: 0.25rem; padding-inline: 0; text-decoration: underline; }
    .preview { position: relative; z-index: 1; border-top: 2px solid var(--ink); margin-top: 0.5rem; padding-top: 0.75rem; }
    .preview h4 { font-size: var(--step-0); margin-bottom: 0.25rem; }
    .domains { margin: 0 0 0.75rem; padding-left: 1.25rem; font-size: var(--step--1); }
    .domains li { display: flex; justify-content: space-between; gap: 1rem; padding: 0.15rem 0; border-bottom: 1px dotted var(--rule); }
    .w { font-weight: 600; font-variant-numeric: tabular-nums; }
    @media (hover: hover) and (pointer: fine) {
      .card.open { box-shadow: 0 0 0 1px var(--ink); }
    }
  `,
})
export class ExamCard {
  exam = input.required<CatalogItem>();
  protected hover = signal(false);
  protected focus = signal(false);
  protected toggled = signal(false);
  protected open = computed(() => this.hover() || this.focus() || this.toggled());
  protected previewId = computed(() => 'preview-' + this.exam().code);

  protected languageLabel = computed(() =>
    this.exam().languageStatus === 'verified' ? this.exam().languages.map((l) => (l === 'en' ? 'English' : l)).join(', ') : 'Pending verification');

  protected availabilityLabel = computed(() => {
    const a = this.exam().availability;
    if (a.publishedQuestions === 0) return 'In review, none published yet';
    if (!a.fullSimulationAvailable) return `${a.publishedQuestions} published, practice only`;
    return `${a.publishedQuestions} published, full simulation ready`;
  });

  protected onHover(on: boolean) {
    // Hover preview only for precise pointers; touch users get the Quick look button.
    if (!on || window.matchMedia('(hover: hover) and (pointer: fine)').matches) this.hover.set(on);
  }

  protected onFocusOut(e: FocusEvent) {
    const card = e.currentTarget as HTMLElement;
    if (!card.contains(e.relatedTarget as Node)) this.focus.set(false);
  }
}
