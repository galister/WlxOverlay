//
// Created by galister on 9/25/22.
//

#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <inttypes.h>
#include "X11/Xlib.h"
#include <xcb/randr.h>
#include <xcb/shm.h>
#include <xcb/xinerama.h>

#include "xhelpers.h"

#define TEX_INTERNAL_FORMAT GL_BGRA
#define TEX_EXTERNAL_FORMAT GL_BGR


struct vec2i_t {
    int32_t x;
    int32_t y;
};

struct buf_t {
    int32_t length;
    void *buffer;
};

struct xshm_data {
    xcb_connection_t *xcb;
    xcb_screen_t *xcb_screen;
    xcb_shm_t *xshm;

    char *server;
    uint_fast32_t screen_id;
    int_fast32_t x_org;
    int_fast32_t y_org;
    int_fast32_t width;
    int_fast32_t height;

    int_fast32_t cut_top;
    int_fast32_t cut_left;
    int_fast32_t cut_right;
    int_fast32_t cut_bot;

    int_fast32_t adj_x_org;
    int_fast32_t adj_y_org;
    int32_t adj_width;
    int_fast32_t adj_height;

    bool use_xinerama;
    bool use_randr;

    struct buf_t buffer;
};

bool xshm_check_extensions(xcb_connection_t *xcb)
{
    bool ok = true;

    if (!xcb_get_extension_data(xcb, &xcb_shm_id)->present) {
        printf("Missing SHM extension !");
        ok = false;
    }

    if (!xcb_get_extension_data(xcb, &xcb_xinerama_id)->present)
        printf("Missing Xinerama extension !");

    if (!xcb_get_extension_data(xcb, &xcb_randr_id)->present)
        printf("Missing Randr extension !");

    return ok;
}

/**
 * Update the capture
 *
 * @return < 0 on error, 0 when size is unchanged, > 1 on size change
 */
int_fast32_t xshm_update_geometry(struct xshm_data *data)
{
    int_fast32_t prev_width = data->adj_width;
    int_fast32_t prev_height = data->adj_height;

    if (data->use_randr) {
        if (randr_screen_geo(data->xcb, data->screen_id, &data->x_org,
                             &data->y_org, &data->width, &data->height,
                             &data->xcb_screen, NULL) < 0) {
            return -1;
        }
    } else if (data->use_xinerama) {
        if (xinerama_screen_geo(data->xcb, data->screen_id,
                                &data->x_org, &data->y_org,
                                &data->width, &data->height) < 0) {
            return -1;
        }
        data->xcb_screen = xcb_get_screen(data->xcb, 0);
    } else {
        data->x_org = 0;
        data->y_org = 0;
        if (x11_screen_geo(data->xcb, data->screen_id, &data->width,
                           &data->height) < 0) {
            return -1;
        }
        data->xcb_screen = xcb_get_screen(data->xcb, data->screen_id);
    }

    if (!data->width || !data->height) {
        printf("Failed to get geometry");
        return -1;
    }

    data->adj_y_org = data->y_org;
    data->adj_x_org = data->x_org;
    data->adj_height = data->height;
    data->adj_width = data->width;

    if (data->cut_top != 0) {
        if (data->y_org > 0)
            data->adj_y_org = data->y_org + data->cut_top;
        else
            data->adj_y_org = data->cut_top;
        data->adj_height = data->adj_height - data->cut_top;
    }
    if (data->cut_left != 0) {
        if (data->x_org > 0)
            data->adj_x_org = data->x_org + data->cut_left;
        else
            data->adj_x_org = data->cut_left;
        data->adj_width = data->adj_width - data->cut_left;
    }
    if (data->cut_right != 0)
        data->adj_width = data->adj_width - data->cut_right;
    if (data->cut_bot != 0)
        data->adj_height = data->adj_height - data->cut_bot;

    printf(
         "Geometry %" PRIdFAST32 "x%" PRIdFAST32 " @ %" PRIdFAST32
         ",%" PRIdFAST32,
         data->width, data->height, data->x_org, data->y_org);

    if (prev_width == data->adj_width && prev_height == data->adj_height)
        return 0;

    return 1;
}

int32_t wlxshm_num_screens()
{
    xcb_connection_t * xcb = xcb_connect(NULL, NULL);
    if (!xcb || xcb_connection_has_error(xcb)) {
        printf("Unable to open X display !");
        return 0;
    }

    int retval = 1;
    if (randr_is_active(xcb))
        retval = randr_screen_count(xcb);
    else if (xinerama_is_active(xcb))
        retval =  xinerama_screen_count(xcb);

    xcb_disconnect(xcb);
    return retval;
}

void wlxshm_destroy(struct xshm_data * data) {
    if (!data)
        return;

    if (data->xshm) {
        xshm_xcb_detach(data->xshm);
        data->xshm = NULL;
    }

    if (data->xcb) {
        xcb_disconnect(data->xcb);
        data->xcb = NULL;
    }

    if (data->server) {
        free(data->server);
        data->server = NULL;
    }

    free(data);
}

struct xshm_data * wlxshm_create(int32_t screen,  struct vec2i_t *size,  struct vec2i_t *pos)
{
    struct xshm_data * data = calloc(1, sizeof(struct xshm_data));
    data->screen_id = screen;

    data->xcb = xcb_connect(NULL, NULL);
    if (!data->xcb || xcb_connection_has_error(data->xcb)) {
        printf("FATAL unable to open X display");
        goto fail;
    }

    if (!xshm_check_extensions(data->xcb)) {
        printf("FATAL xcb extension not supported");
        goto fail;
    }

    data->use_randr = randr_is_active(data->xcb) ? true : false;
    data->use_xinerama = xinerama_is_active(data->xcb) ? true : false;

    if (xshm_update_geometry(data) < 0) {
        printf("failed to update geometry");
    }

    size->x = (int) data->adj_width;
    size->y = (int) data->adj_height;

    size->x = (int) data->adj_x_org;
    size->y = (int) data->adj_y_org;

    return data;

    fail:
    wlxshm_destroy(data);
    return NULL;
}


void wlxshm_mouse_pos_global(struct xshm_data * data, struct vec2i_t *vec)
{
    xcb_query_pointer_cookie_t xp_c =
            xcb_query_pointer_unchecked(data->xcb, data->xcb_screen->root);
    xcb_query_pointer_reply_t *xp =
            xcb_query_pointer_reply(data->xcb, xp_c, NULL);

    if (!xp)
        return;

    vec->x = xp->root_x;
    vec->y = xp->root_y;

    free(xp);
}

int32_t wlxshm_capture_start(struct xshm_data * data){

    data->xshm = xshm_xcb_attach(data->xcb, data->adj_width, data->adj_height);
    if (!data->xshm) {
        printf("FATAL failed to attach shm");
        wlxshm_destroy(data);
        return 1;
    }

    return 0;
}

void wlxshm_capture_end(struct xshm_data * data){

    xshm_xcb_detach(data->xshm);
    data->xshm = NULL;
}

struct buf_t * wlxshm_capture_frame(struct xshm_data * data)
{
    if (!data->xshm)
        goto Empty;

    xcb_shm_get_image_cookie_t img_c;
    xcb_shm_get_image_reply_t *img_r;

    img_c = xcb_shm_get_image_unchecked(data->xcb, data->xcb_screen->root,
                                        data->adj_x_org, data->adj_y_org,
                                        data->adj_width, data->adj_height,
                                        ~0, XCB_IMAGE_FORMAT_Z_PIXMAP,
                                        data->xshm->seg, 0);

    img_r = xcb_shm_get_image_reply(data->xcb, img_c, NULL);

    if (img_r) {
        data->buffer.length = img_r->size;
        data->buffer.buffer = data->xshm->data;
        free(img_r);
        return &data->buffer;
    }

    Empty:
    data->buffer.length = 0;
    data->buffer.buffer = 0;
    return &data->buffer;
}