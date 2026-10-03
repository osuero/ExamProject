import { Component, input } from '@angular/core';
import { DomainSummary } from '../core/api';

/** Stacked bar of domain weights: the shape of the exam at a glance. Values also listed in text nearby. */
@Component({
  selector: 'app-weight-bar',
  template: `
    <div class="bar" role="img" [attr.aria-label]="label()">
      @for (d of domains(); track d.code; let i = $index) {
        <span class="seg" [style.flex-grow]="d.weightPercent ?? 0" [style.background]="tone(i)" [title]="d.name + ': ' + d.weightPercent + '%'"></span>
      }
    </div>
  `,
  styles: `
    .bar { display: flex; height: 10px; gap: 2px; margin: 0.25rem 0; }
    .seg { display: block; min-width: 3px; border-radius: 1px; }
  `,
})
export class WeightBar {
  domains = input.required<DomainSummary[]>();
  private tones = ['#14213d', '#2747d6', '#6d83e8', '#a9b7f2', '#4a5468', '#8b95a8', '#c3cad6', '#1d3a8a'];
  tone(i: number) { return this.tones[i % this.tones.length]; }
  label() { return 'Domain weights: ' + this.domains().map((d) => `${d.name} ${d.weightPercent}%`).join(', '); }
}
