import { NgModule } from "@angular/core";
import { AppInputComponent } from "./app-input.component";
import { AppOptionComponent } from "./app-option.component";
import { AppSelectComponent } from "./app-select.component";
import { AppTextAreaComponent } from "./app-text-area.component";

/** Single import surface for the shared control kit: app-input, app-select, app-text-area. */
@NgModule({
  imports: [
    AppInputComponent,
    AppOptionComponent,
    AppSelectComponent,
    AppTextAreaComponent,
  ],
  exports: [
    AppInputComponent,
    AppOptionComponent,
    AppSelectComponent,
    AppTextAreaComponent,
  ],
})
export class AppFormControlsModule {}
