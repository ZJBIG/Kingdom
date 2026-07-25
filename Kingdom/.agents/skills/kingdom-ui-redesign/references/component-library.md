# UI component library

Create shared Prefabs only after the UI-Ready gate:

- PrimaryButton
- SecondaryButton
- DangerButton
- IconButton
- TabButton
- TextInput
- Toggle
- ProgressBar
- ResourceRequirementView
- StatusBadge
- Tooltip
- ToastView
- ConfirmDialog
- EmptyState
- SectionHeader

Rules:

- components receive data through Bind/configuration methods;
- components do not locate Managers;
- components do not own gameplay State;
- use explicit serialized references;
- support normal/hover/pressed/selected/disabled states;
- preserve large-number readability;
- avoid duplicated near-identical Prefabs.
