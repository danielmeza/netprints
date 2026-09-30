# Accessibility and validation

Automation names and announcements, and control-level validation visuals.

Part of the Xaml.Behaviors 12.0.7 catalog; `README.md` in this folder is the index. Prebuilt types need no xmlns prefix.

## Catalog

### Automation/Actions · `Xaml.Behaviors.Interactions.Custom`

Use when: Screen-reader announcements or runtime AutomationId [ScreenReaderAnnounceAction for status changes].

| Name | Kind | What it does |
|---|---|---|
| `ScreenReaderAnnounceAction` | Action | An action that requests a screen reader announcement. |
| `SetAutomationIdAction` | Action | Sets AutomationProperties.AutomationIdProperty on the target control when executed. |

### Automation/Behaviors · `Xaml.Behaviors.Interactions.Custom`

Use when: Bind AutomationProperties.Name from another source [rarely needed; set the attached property].

| Name | Kind | What it does |
|---|---|---|
| `AutomationNameBehavior` | Behavior | Sets AutomationProperties.NameProperty on the associated control when attached. |

### Automation/Triggers · `Xaml.Behaviors.Interactions.Custom`

Use when: React when an automation name changes [rare].

| Name | Kind | What it does |
|---|---|---|
| `AutomationNameChangedTrigger` | Trigger | Executes actions when AutomationProperties.NameProperty of the associated control changes. |

### Validation · `Xaml.Behaviors.Interactions.Custom`

Use when: Control-level validation visuals [avoid for rules: validation logic stays in the VM; INotifyDataErrorInfo].

| Name | Kind | What it does |
|---|---|---|
| `ComboBoxValidationBehavior` | Behavior | Validation behavior for ComboBox selected item. |
| `DatePickerValidationBehavior` | Behavior | Validation behavior for DatePicker selected date. |
| `NumericUpDownValidationBehavior` | Behavior | Validation behavior for NumericUpDown value. |
| `PropertyValidationBehavior<TControl, TValue>` | Behavior | Base behavior that validates a property value using a set of rules. |
| `SliderValidationBehavior` | Behavior | Validation behavior for range based controls like Slider value. |
| `TextBoxValidationBehavior` | Behavior | Validation behavior for TextBox text. |
