#ifndef HORIZONTAL_MAPPER_H
#define HORIZONTAL_MAPPER_H

#include "pixel-mapper.h"

using namespace rgb_matrix;

class HorizontalMapper : public PixelMapper {
public:
  HorizontalMapper();
  virtual ~HorizontalMapper();

  virtual const char *GetName() const override;
  virtual bool SetParameters(int chain, int parallel, const char *parameter_string) override;
  virtual bool GetSizeMapping(int matrix_width, int matrix_height,
                              int *visible_width, int *visible_height) const override;
  virtual void MapVisibleToMatrix(int matrix_width, int matrix_height,
                                  int visible_x, int visible_y,
                                  int *matrix_x, int *matrix_y) const override;

private:
  int chain_length_;
  int parallel_;
};

#endif // HORIZONTAL_MAPPER_H

//make sure to add #include "../horizontal_mapper.h" to the top of
// lib/pixel-mapper.cc (after other #includes)