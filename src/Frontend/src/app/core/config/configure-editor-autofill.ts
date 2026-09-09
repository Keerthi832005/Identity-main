import Autocomplete from "devextreme/ui/autocomplete";
import DateBox from "devextreme/ui/date_box";
import NumberBox from "devextreme/ui/number_box";
import SelectBox from "devextreme/ui/select_box";
import TextArea from "devextreme/ui/text_area";
import TextBox from "devextreme/ui/text_box";
import { browserAutofillAttributes } from "./browser-autofill";

/** Register before bootstrap, including editors created by grids and popups. */
export function configureEditorAutofill(): void {
  const rule = { options: { inputAttr: browserAutofillAttributes() } };
  Autocomplete.defaultOptions(rule);
  DateBox.defaultOptions(rule);
  NumberBox.defaultOptions(rule);
  SelectBox.defaultOptions(rule);
  TextArea.defaultOptions(rule);
  TextBox.defaultOptions(rule);
}
