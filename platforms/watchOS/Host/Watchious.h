#ifndef WATCHIOUS_H
#define WATCHIOUS_H

#include <stdint.h>

typedef void (*watchious_text_callback)(const uint8_t *utf8, int32_t length);

int32_t watchious_start(watchious_text_callback render, watchious_text_callback report_error);

int32_t watchious_dispatch(uint64_t event_handler_id, const char *event_name, const char *value);

#endif
