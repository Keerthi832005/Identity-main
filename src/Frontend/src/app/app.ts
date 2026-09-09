import { Component, inject } from "@angular/core";
import { RouterOutlet } from "@angular/router";
import { ThemePreferenceService } from "./core/theme/theme-preference.service";
import { AppConfirmationComponent } from "./shared/ui/confirmation/app-confirmation.component";
import { ConfirmationService } from "./shared/ui/confirmation/confirmation.service";

@Component({
  selector: "app-root",
  imports: [RouterOutlet, AppConfirmationComponent],
  template: `<router-outlet />
    @if (confirmations.active(); as request) {
      <app-confirmation
        [request]="request"
        (decided)="confirmations.respond(request, $event)"
      />
    }`,
})
export class App {
  private readonly theme = inject(ThemePreferenceService);
  protected readonly confirmations = inject(ConfirmationService);
}
