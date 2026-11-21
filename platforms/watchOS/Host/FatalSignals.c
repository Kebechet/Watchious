// watchOS on a real watch aborts any app that installs a handler for a fatal signal ("sigaction on fatal signals is
// not supported"), and the NativeAOT runtime installs one for SIGSEGV at startup (InitializeHardwareExceptionHandling)
// to turn memory faults into NullReferenceException. The runtime is linked statically into the app, so this
// definition is the one it calls instead of libSystem's: fatal signals keep their default action, the runtime is told
// its handler was installed, and every other signal goes to the real sigaction.
// The cost: on watchOS, a null dereference in C# ends the app instead of throwing NullReferenceException.
#include <dlfcn.h>
#include <signal.h>
#include <stddef.h>
#include <string.h>

typedef int (*SigactionFunction)(int, const struct sigaction *, struct sigaction *);

static int IsFatalSignal(int signalNumber)
{
    switch (signalNumber)
    {
        case SIGSEGV:
        case SIGBUS:
        case SIGILL:
        case SIGFPE:
        case SIGTRAP:
        case SIGABRT:
        case SIGSYS:
            return 1;
        default:
            return 0;
    }
}

int sigaction(int signalNumber, const struct sigaction *restrict action, struct sigaction *restrict previousAction)
{
    if (IsFatalSignal(signalNumber))
    {
        if (previousAction != NULL)
        {
            memset(previousAction, 0, sizeof(*previousAction));
        }
        return 0;
    }

    static SigactionFunction systemSigaction;
    if (systemSigaction == NULL)
    {
        systemSigaction = (SigactionFunction)dlsym(RTLD_NEXT, "sigaction");
    }
    return systemSigaction(signalNumber, action, previousAction);
}
