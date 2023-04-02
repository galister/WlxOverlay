//
// Created by op on 4/2/23.
//

#ifndef WLXPW_HELPERS_H
#define WLXPW_HELPERS_H

struct spa_pod *build_format(struct spa_pod_builder *b,
                             uint32_t fps,
                             uint32_t format, uint64_t *modifiers,
                             size_t modifier_count);

#endif //WLXPW_HELPERS_H
