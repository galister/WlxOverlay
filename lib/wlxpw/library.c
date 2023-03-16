#include <spa/param/video/format-utils.h>
#include <spa/debug/types.h>
#include <spa/param/video/type-info.h>

#include <pipewire/pipewire.h>

struct wlxpw {
    struct pw_thread_loop * loop;
    struct pw_context * context;
    struct pw_core * core;
    struct pw_stream * stream;
    struct spa_video_info format;
    struct spa_hook listener;
    void (*on_frame)(struct spa_buffer *, struct spa_video_info *);
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
        return;
    }

    buf = b->buffer;
    if (buf->datas[0].data == NULL)
        return;

    if (buf->datas[0].chunk->size > 0) {
        data->on_frame(buf, &data->format);
    }
    pw_stream_queue_buffer(data->stream, b);
}

static void on_param_changed(void *userdata, uint32_t id, const struct spa_pod *param)
{
    struct wlxpw *data = userdata;

    if (param == NULL || id != SPA_PARAM_Format)
        return;

    if (spa_format_parse(param,
                         &data->format.media_type,
                         &data->format.media_subtype) < 0)
        return;

    if (data->format.media_type != SPA_MEDIA_TYPE_video ||
        data->format.media_subtype != SPA_MEDIA_SUBTYPE_raw)
        return;

    if (spa_format_video_raw_parse(param, &data->format.info.raw) < 0)
        return;

    printf("got video format:\n");
    printf("  format: %d (%s)\n", data->format.info.raw.format,
           spa_debug_type_find_name(spa_type_video_format,
                                    data->format.info.raw.format));
    printf("  size: %dx%d\n", data->format.info.raw.size.width,
           data->format.info.raw.size.height);
    printf("  framerate: %d/%d\n", data->format.info.raw.framerate.num,
           data->format.info.raw.framerate.denom);

}

static const struct pw_stream_events stream_events = {
        PW_VERSION_STREAM_EVENTS,
        .param_changed = on_param_changed,
        .process = on_process,
};

struct wlxpw * wlxpw_initialize(const char * name, uint32_t node_id, int32_t hz, int32_t num_modifiers, uint64_t *modifiers, void * on_frame)
{
    struct wlxpw* data = malloc(sizeof(struct wlxpw));
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

    struct pw_properties * props = pw_properties_new_string("media.type=Video media.category=Capture media.role=Screen");

    data->stream = pw_stream_new(data->core, name, props);
    if (data->stream == 0) {
        printf("Failed @ pw_stream_new!");
        pw_thread_loop_unlock(data->loop);
        free(data);
        return NULL;
    }

    uint8_t buffer[4096];
    struct spa_pod_builder b = SPA_POD_BUILDER_INIT(buffer, sizeof(buffer));
    struct spa_pod_frame frame;
    spa_pod_builder_push_object(&b, &frame, SPA_TYPE_OBJECT_Format, SPA_PARAM_EnumFormat);
    spa_pod_builder_add(&b, SPA_FORMAT_mediaType, SPA_POD_Id(SPA_MEDIA_TYPE_video), 0);
    spa_pod_builder_add(&b, SPA_FORMAT_mediaSubtype, SPA_POD_Id(SPA_MEDIA_SUBTYPE_raw), 0);
    spa_pod_builder_add(&b, SPA_FORMAT_VIDEO_format, SPA_POD_CHOICE_ENUM_Id(4,
            SPA_VIDEO_FORMAT_RGBA,
            SPA_VIDEO_FORMAT_BGRA,
            SPA_VIDEO_FORMAT_RGBx,
            SPA_VIDEO_FORMAT_BGRx), 0);

    spa_pod_builder_add(&b, SPA_FORMAT_VIDEO_size, SPA_POD_CHOICE_RANGE_Rectangle(
            &SPA_RECTANGLE(320, 240),
            &SPA_RECTANGLE(1, 1),
            &SPA_RECTANGLE(8192, 8192)), 0);

    spa_pod_builder_add(&b, SPA_FORMAT_VIDEO_framerate, SPA_POD_CHOICE_RANGE_Fraction(
        &SPA_FRACTION(hz, 1),
        &SPA_FRACTION(0, 1),
        &SPA_FRACTION(1000, 1)), 0);

    if (num_modifiers > 0) {
        spa_pod_builder_prop(&b, SPA_FORMAT_VIDEO_modifier,
                             SPA_POD_PROP_FLAG_MANDATORY | SPA_POD_PROP_FLAG_DONT_FIXATE);

        struct spa_pod_frame subframe;
        spa_pod_builder_push_choice(&b, &subframe, SPA_CHOICE_Enum, 0);
        for (int i = 0; i < num_modifiers; i++)
            spa_pod_builder_long(&b, modifiers[i]);

        spa_pod_builder_pop(&b, &subframe);
    }
    void * params = spa_pod_builder_pop(&b, &frame);

    pw_stream_add_listener(data->stream, &data->listener, &stream_events, data);
    pw_stream_connect(data->stream, PW_DIRECTION_INPUT, node_id, PW_STREAM_FLAG_AUTOCONNECT | PW_STREAM_FLAG_MAP_BUFFERS, params, 1);

    pw_thread_loop_unlock(data->loop);
    return data;
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
}