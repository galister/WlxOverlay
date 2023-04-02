#include <spa/param/video/format-utils.h>
#include <spa/debug/types.h>
#include <spa/param/video/type-info.h>

#include <pipewire/pipewire.h>
#include "helpers.h"

struct wlxpw {
    char name[32];
    struct pw_thread_loop * loop;
    struct pw_context * context;
    struct pw_core * core;
    struct pw_stream * stream;
    struct spa_video_info format;
    struct spa_hook listener;
    uint_fast8_t want_dmabuf;
    void (*on_frame)(struct spa_buffer *, struct spa_video_info *);
};

struct format_collection {
    int32_t num_formats;
    struct capture_format * formats;
};

struct capture_format {
    uint32_t format;
    int32_t num_modifiers;
    uint64_t * modifiers;
};

static void on_process(void *userdata)
{
    struct wlxpw *data = userdata;
    struct pw_buffer *b;
    struct spa_buffer *buf;

    b = NULL;
    while (1) {
        struct pw_buffer *swap = pw_stream_dequeue_buffer(data->stream);
        if (!swap)
            break;
        if (b)
            pw_stream_queue_buffer(data->stream, b);
        b = swap;
    }

    if (b == NULL) {
        printf("PipeWire: %s ran out of buffers\n", data->name);
        return;
    }

    buf = b->buffer;
    if (buf->datas[0].chunk->size > 0)
        data->on_frame(buf, &data->format);

    pw_stream_queue_buffer(data->stream, b);
}

static void on_param_changed(void *userdata, uint32_t id, const struct spa_pod *param)
{
    struct wlxpw *data = userdata;

    if (param == NULL || id != SPA_PARAM_Format)
        return;

    if (spa_format_parse(param,
                         &data->format.media_type,
                         &data->format.media_subtype) < 0) {
        printf("PipeWire: Failed to parse format.\n");
        return;
    }

    if (data->format.media_type != SPA_MEDIA_TYPE_video ||
        data->format.media_subtype != SPA_MEDIA_SUBTYPE_raw) {
        printf("PipeWire: Wrong media type: %s %s\n",
               spa_debug_type_find_name(spa_type_media_type, data->format.media_type),
               spa_debug_type_find_name(spa_type_media_subtype, data->format.media_subtype));
        return;
    }

    if (spa_format_video_raw_parse(param, &data->format.info.raw) < 0) {
        printf("PipeWire: Failed to parse raw video format.\n");
        return;
    }

    bool has_mod = spa_pod_find_prop(param, NULL, SPA_FORMAT_VIDEO_modifier) != NULL;

    printf("PipeWire: %s got format:\n", data->name);
    printf("  format: %d (%s)\n", data->format.info.raw.format,
           spa_debug_type_find_name(spa_type_video_format,data->format.info.raw.format));
    if (has_mod)
        printf("  mod: %lu\n", data->format.info.raw.modifier);
    printf("  size: %dx%d\n", data->format.info.raw.size.width,
           data->format.info.raw.size.height);
    printf("  framerate: %d/%d\n", data->format.info.raw.framerate.num,
           data->format.info.raw.framerate.denom);

    uint8_t buffer[1024];
    struct spa_pod_builder b = SPA_POD_BUILDER_INIT(buffer, sizeof(buffer));
    const struct spa_pod *params[1];

    uint32_t data_types = (1 << SPA_DATA_MemFd | 1 << SPA_DATA_MemPtr);

    if (has_mod || data->want_dmabuf)
        data_types |= 1 << SPA_DATA_DmaBuf;

    params[0] = spa_pod_builder_add_object(&b,
        SPA_TYPE_OBJECT_ParamBuffers, SPA_PARAM_Buffers,
        SPA_PARAM_BUFFERS_dataType, SPA_POD_Int(data_types));

    pw_stream_update_params(data->stream, params, 1);
}

static void on_state_changed(void *userdata, enum pw_stream_state _,
                                enum pw_stream_state new, const char *err)
{
    struct wlxpw *data = userdata;
    printf("PipeWire: %s %s (%s)\n", data->name, pw_stream_state_as_string(new), err ? err : "ok");
}


static const struct pw_stream_events stream_events = {
        PW_VERSION_STREAM_EVENTS,
        .state_changed = on_state_changed,
        .param_changed = on_param_changed,
        .process = on_process,
};

struct wlxpw * wlxpw_initialize(const char * name, uint32_t node_id, uint32_t fps, struct format_collection * formats, void * on_frame)
{
    struct wlxpw* data = malloc(sizeof(struct wlxpw));
    strcpy(data->name, name);

    data->want_dmabuf = formats != NULL;
    data->on_frame = on_frame;

    pw_init(0, NULL);

    data->loop = pw_thread_loop_new(name, 0);
    if (data->loop == 0) {
        printf("Failed @ pw_thread_loop_new!");
        free(data);
        return NULL;
    }

    data->context = pw_context_new(pw_thread_loop_get_loop(data->loop), 0, 0);
    if (data->context == 0) {
        printf("Failed @ pw_context_new!");
        free(data);
        return NULL;
    }

    pw_thread_loop_start(data->loop);

    pw_thread_loop_lock(data->loop);

    data->core = pw_context_connect(data->context, 0, 0);
    if (data->core == 0) {
        printf("Failed @ pw_context_connect!");
        pw_thread_loop_unlock(data->loop);
        free(data);
        return NULL;
    }

    struct pw_properties * props = pw_properties_new(PW_KEY_MEDIA_TYPE, "Video",
                      PW_KEY_MEDIA_CATEGORY, "Capture",
                      PW_KEY_MEDIA_ROLE, "Screen", NULL);

    data->stream = pw_stream_new(data->core, name, props);
    if (data->stream == 0) {
        printf("Failed @ pw_stream_new!");
        pw_thread_loop_unlock(data->loop);
        free(data);
        return NULL;
    }

    uint8_t buffer[4096];
    struct spa_pod_builder b = SPA_POD_BUILDER_INIT(buffer, sizeof(buffer));
    int num_dmabuf_formats = data->want_dmabuf ? formats->num_formats : 0;
    const struct spa_pod *params[num_dmabuf_formats + 1];
    int p = 0;

    if (data->want_dmabuf)
        for (int f = 0; f < formats->num_formats; f++) {
            struct capture_format * cur_format = &formats->formats[f];
            if (cur_format->num_modifiers < 1)
                continue;

            params[p++] = build_format(&b, fps,
                                       cur_format->format,
                                       cur_format->modifiers,
                                       cur_format->num_modifiers);
        }

    params[p++] = spa_pod_builder_add_object(&b,
       SPA_TYPE_OBJECT_Format, SPA_PARAM_EnumFormat,
       SPA_FORMAT_mediaType,   SPA_POD_Id(SPA_MEDIA_TYPE_video),
       SPA_FORMAT_mediaSubtype,SPA_POD_Id(SPA_MEDIA_SUBTYPE_raw),
       SPA_FORMAT_VIDEO_format,SPA_POD_CHOICE_ENUM_Id(4,
            SPA_VIDEO_FORMAT_RGBA,
            SPA_VIDEO_FORMAT_BGRA,
            SPA_VIDEO_FORMAT_RGBx,
            SPA_VIDEO_FORMAT_BGRx),
       SPA_FORMAT_VIDEO_size,      SPA_POD_CHOICE_RANGE_Rectangle(
            &SPA_RECTANGLE(320, 240),
            &SPA_RECTANGLE(1, 1),
            &SPA_RECTANGLE(8192, 8192)),
    SPA_FORMAT_VIDEO_framerate, SPA_POD_CHOICE_RANGE_Fraction(
        &SPA_FRACTION(fps, 1),
        &SPA_FRACTION(0, 1),
        &SPA_FRACTION(1000, 1)));

    pw_stream_add_listener(data->stream, &data->listener, &stream_events, data);
    pw_stream_connect(data->stream, PW_DIRECTION_INPUT, node_id, PW_STREAM_FLAG_AUTOCONNECT | PW_STREAM_FLAG_MAP_BUFFERS, params, p);

    pw_thread_loop_unlock(data->loop);
    return data;
}

void wlxpw_set_active(struct wlxpw * data, uint32_t active) {
    if (data && data->stream)
        pw_stream_set_active(data->stream, active);
}

void wlxpw_destroy(struct wlxpw * data) {
    if (data->stream) {
        pw_stream_destroy(data->stream);
        data->stream = NULL;
    }

    if (data->context) {
        pw_context_destroy(data->context);
    }

    if (data->loop) {
        pw_thread_loop_wait(data->loop);
        pw_thread_loop_stop(data->loop);
        pw_thread_loop_destroy(data->loop);
        data->loop = NULL;
    }

    free(data);
}
