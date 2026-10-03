import { Component, inject, OnInit, signal } from '@angular/core';
import { Api, CatalogItem, toApiError } from '../core/api';
import { ExamCard } from '../shared/exam-card';

@Component({
  selector: 'app-catalog',
  imports: [ExamCard],
  template: `
    <div class="page">
      <h1>Exams</h1>
      <p class="muted">Each exam has its own question bank, syllabus and history. One account works for all of them.</p>
      @if (error()) { <div class="notice error" role="alert">{{ error() }}</div> }
      @if (loading()) { <p aria-live="polite">Loading exams…</p> }
      <div class="grid">
        @for (e of exams(); track e.code) { <app-exam-card [exam]="e" /> }
      </div>
    </div>
  `,
  styles: `.grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); gap: 1.25rem; margin-top: 1.5rem; }`,
})
export class CatalogPage implements OnInit {
  private api = inject(Api);
  protected exams = signal<CatalogItem[]>([]);
  protected loading = signal(true);
  protected error = signal<string | null>(null);
  async ngOnInit() {
    try { this.exams.set(await this.api.catalog()); }
    catch (e) { this.error.set(toApiError(e).message); }
    finally { this.loading.set(false); }
  }
}
