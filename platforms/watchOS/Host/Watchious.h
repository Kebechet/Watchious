#ifndef WATCHIOUS_H
#define WATCHIOUS_H

#include <stdint.h>

typedef void (*watchious_text_callback)(const uint8_t *utf8, int32_t length);

// One batch of patches (protocol v2, see Watchious.Core's Protocol/PatchOperation.cs), valid only during the call.
typedef void (*watchious_patches_callback)(const uint8_t *patches, int32_t length);

void watchious_start_runtime(void);

int32_t watchious_start(watchious_patches_callback apply_patches, watchious_text_callback report_error);

int32_t watchious_dispatch(uint64_t event_handler_id, const char *event_name, const char *value);

#endif
