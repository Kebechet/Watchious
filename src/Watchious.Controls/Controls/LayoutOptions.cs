namespace Watchious.Controls;

// MAUI's LayoutOptions, a control's HorizontalOptions and VerticalOptions: where it sits in the space its parent gives
// it. As in MAUI, a stack ignores its children's options along its own direction: a VStack (and a ScrollView, a List
// row) places a child by its HorizontalOptions, an HStack by its VerticalOptions. A page without a List or ScrollView
// places its content by both and centers it otherwise; a child with VerticalOptions there takes an equal share of the
// height the others leave. Start, Center and End keep the control's own size. Fill stretches a Button, ProgressView,
// TextField or labeled Toggle across the width, and gives any other control the whole space to sit centered in.
// Without options a control stays where Watchious puts it: centered, with Wear OS's Button, ProgressView, TextField and
// labeled Toggle across the width of a vertical stack.
public enum LayoutOptions
{
    Start,
    Center,
    End,
    Fill,
}
