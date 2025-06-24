#ifndef MY_HORIZONTAL_MAPPER_H
#define MY_HORIZONTAL_MAPPER_H

#include "pixel-mapper.h"

class HorizontalMapper : public rgb_matrix::PixelMapper {
public:
  const char *GetName() const override;
  bool SetParameters(int chain, int parallel, const char *parameter_string) override;
  bool GetSizeMapping(int matrix_width, int matrix_height, int *visible_width, int *visible_height) const override;
  void MapVisibleToMatrix(int matrix_width, int matrix_height, int visible_x, int visible_y, int *matrix_x, int *matrix_y) const override;

private:
  int chain_length_;
  int parallel_;
};

#endif // MY_HORIZONTAL_MAPPER_H
