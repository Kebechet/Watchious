// The watch app's C# is built by .NET for iOS, whose runtime glue (libxamarin) connects C# to Objective-C and
// so makes Apple's frameworks usable from C#. On an iPhone, its generated main() starts that glue through
// xamarin_main, which then runs the app's managed Main and UIKit. A watch app has neither, so this runs the same
// start-up steps as xamarin_main (dotnet/macios runtime/monotouch-main.m) and stops before Main.
// ⚠️ These are internal functions of .NET for iOS, so a new .NET for iOS version can change them.
#include <stddef.h>
#include "Watchious.h"

typedef void (*XamarinCallback)(void);
extern XamarinCallback xamarin_setup;
extern XamarinCallback xamarin_register_assemblies;

void xamarin_bridge_setup(void);
void xamarin_initialize_cocoa_threads(void *callback);
void xamarin_vm_initialize(void);
void xamarin_bridge_initialize(void);
void xamarin_initialize(void);

void watchious_start_runtime(void)
{
    xamarin_setup();
    xamarin_bridge_setup();
    xamarin_initialize_cocoa_threads(NULL);
    xamarin_vm_initialize();
    xamarin_bridge_initialize();
    xamarin_initialize();
    if (xamarin_register_assemblies != NULL)
    {
        xamarin_register_assemblies();
    }
}
