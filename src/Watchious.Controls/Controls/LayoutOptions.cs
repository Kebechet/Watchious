namespace Watchious.Controls;

// MAUI's LayoutOptions, a control's HorizontalOptions and VerticalOptions: where it sits in the space its parent gives
// it. As in MAUI, a stack ignores its children's options along its own direction: a VStack (and a ScrollView, a List
// row) places a child by its HorizontalOptions within its own width, an HStack by its VerticalOptions within the row's
// height. A VStack spans the width it is given unless it is in an HStack or its own Start, Center or End keep it as wide
// as its widest child. A page without a List or ScrollView places its content by both and centers it otherwise; a child
// with VerticalOptions there takes an equal share of the height the others leave. Start, Center and End keep the
// control's own size. Fill stretches a VStack, Button, TextField or labeled Toggle across the width and centers any
// other control; nothing stretches vertically, and a linear ProgressView always spans the width. Without options a
// control stays where Watchious puts it: centered, with a Button, TextField, labeled Toggle or linear ProgressView
// across the width of a vertical stack.
public enum LayoutOptions
{
    Start,
    Center,
    End,
    Fill,
}
