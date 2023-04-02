/* exert from pipewire.c
 * https://github.com/obsproject/obs-studio/blob/584de6b2646320ea46b600b56b01965a87136810/plugins/linux-pipewire/pipewire.c
 *
 * Copyright 2020 Georges Basile Stavracas Neto <georges.stavracas@gmail.com>
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 *
 * SPDX-License-Identifier: GPL-2.0-or-later
 */

#include <spa/param/video/format-utils.h>
#include <spa/param/video/type-info.h>

struct spa_pod *build_format(struct spa_pod_builder *b,
                               uint32_t fps,
                               uint32_t format, uint64_t *modifiers,
                               size_t modifier_count)
{
    struct spa_pod_frame format_frame;

    /* Make an object of type SPA_TYPE_OBJECT_Format and id SPA_PARAM_EnumFormat.
     * The object type is important because it defines the properties that are
     * acceptable. The id gives more context about what the object is meant to
     * contain. In this case we enumerate supported formats. */
    spa_pod_builder_push_object(b, &format_frame, SPA_TYPE_OBJECT_Format,
                                SPA_PARAM_EnumFormat);
    /* add media type and media subtype properties */
    spa_pod_builder_add(b, SPA_FORMAT_mediaType,
                        SPA_POD_Id(SPA_MEDIA_TYPE_video), 0);
    spa_pod_builder_add(b, SPA_FORMAT_mediaSubtype,
                        SPA_POD_Id(SPA_MEDIA_SUBTYPE_raw), 0);

    /* formats */
    spa_pod_builder_add(b, SPA_FORMAT_VIDEO_format, SPA_POD_Id(format), 0);

    /* modifier */
    if (modifier_count > 0) {
        struct spa_pod_frame modifier_frame;

        /* build an enumeration of modifiers */
        spa_pod_builder_prop(b, SPA_FORMAT_VIDEO_modifier,
                             SPA_POD_PROP_FLAG_MANDATORY |
                             SPA_POD_PROP_FLAG_DONT_FIXATE);

        spa_pod_builder_push_choice(b, &modifier_frame, SPA_CHOICE_Enum,
                                    0);

        /* The first element of choice pods is the preferred value. Here
         * we arbitrarily pick the first modifier as the preferred one.
         */
        spa_pod_builder_long(b, modifiers[0]);

        /* modifiers from  an array */
        for (uint32_t i = 0; i < modifier_count; i++)
            spa_pod_builder_long(b, modifiers[i]);

        spa_pod_builder_pop(b, &modifier_frame);
    }
    /* add size and framerate ranges */
    spa_pod_builder_add(b, SPA_FORMAT_VIDEO_size,
                        SPA_POD_CHOICE_RANGE_Rectangle(
                                &SPA_RECTANGLE(320, 240), // Arbitrary
                                &SPA_RECTANGLE(1, 1),
                                &SPA_RECTANGLE(8192, 4320)),
                        SPA_FORMAT_VIDEO_framerate,
                        SPA_POD_CHOICE_RANGE_Fraction(
                                &SPA_FRACTION(fps, 1),
                                &SPA_FRACTION(0, 1), &SPA_FRACTION(360, 1)),
                        0);
    return spa_pod_builder_pop(b, &format_frame);
}
