#include "horizontal_mapper.h"

HorizontalMapper::HorizontalMapper() {}

HorizontalMapper::~HorizontalMapper() {}

const char *HorizontalMapper::GetName() const {
  return "horizontal_mapper";
}

bool HorizontalMapper::SetParameters(int chain, int parallel, const char *parameter_string) {
  chain_length_ = chain;
  parallel_ = parallel;
  return true;
}

bool HorizontalMapper::GetSizeMapping(int matrix_width, int matrix_height,
                                      int *visible_width, int *visible_height) const {
  int panel_width = 64;
  int panel_height = 64;

  *visible_width = chain_length_ * parallel_ * panel_width;
  *visible_height = panel_height;

  return true;
}

void HorizontalMapper::MapVisibleToMatrix(int matrix_width, int matrix_height,
                                          int visible_x, int visible_y,
                                          int *matrix_x, int *matrix_y) const {
  int panel_width = 64;

  int panel_index = visible_x / panel_width;

  int chain_index = panel_index % chain_length_;
  int parallel_index = panel_index / chain_length_;

  int local_x = visible_x % panel_width;
  int local_y = visible_y + parallel_index * 64;

  *matrix_x = chain_index * panel_width + local_x;
  *matrix_y = local_y;
}
