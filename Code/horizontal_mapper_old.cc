#include "led-matrix.h"
#include "pixel-mapper.h"

using namespace rgb_matrix;

// Custom PixelMapper that arranges all panels in a continuous horizontal layout
class HorizontalMapper : public PixelMapper {
public:
  // Name of this mapper (used on the command line with --led-pixel-mapper=horizontal_mapper)
  virtual const char *GetName() const override {
    return "horizontal_mapper";
  }

  // This method receives the number of panels in each chain and the number of parallel chains.
  // We store this info to use later in the mapping calculation.
  virtual bool SetParameters(int chain, int parallel, const char *parameter_string) override {
    chain_length_ = chain;   // Number of panels connected in series (per chain)
    parallel_ = parallel;    // Number of parallel chains
    return true; // Parameters are successfully parsed
  }

  // This function tells the library what the "visible" size of the display should be.
  // Normally, the library stacks parallel chains vertically, but here we want a wide, single-row display.
  virtual bool GetSizeMapping(int matrix_width, int matrix_height,
                              int *visible_width, int *visible_height) const override {
    int panel_width = 64;  // Width of a single panel (adjust if your panels are a different size)
    int panel_height = 64; // Height of a single panel

    // The visible width is the total number of panels (chain_length * parallel chains) times the panel width
    *visible_width = chain_length_ * parallel_ * panel_width;

    // The visible height is just the height of a single panel (all are arranged horizontally)
    *visible_height = panel_height;

    return true;
  }

  // This is the core mapping function.
  // It tells the library how to translate from the visible coordinate system (what we draw) to the physical LED matrix coordinate system (what's wired).
  virtual void MapVisibleToMatrix(int matrix_width, int matrix_height,
                                  int visible_x, int visible_y,
                                  int *matrix_x, int *matrix_y) const override {
    int panel_width = 64; // Width of one panel

    // Determine which "panel" this pixel belongs to based on visible_x
    int panel_index = visible_x / panel_width;

    // Figure out which chain this panel belongs to
    int chain_index = panel_index % chain_length_;

    // Figure out which parallel chain this panel belongs to
    int parallel_index = panel_index / chain_length_;

    // Local X coordinate within the panel
    int local_x = visible_x % panel_width;

    // Y coordinate needs to be offset depending on which parallel chain we are in
    int local_y = visible_y + parallel_index * 64;

    // Calculate the actual physical coordinates in the matrix
    *matrix_x = chain_index * panel_width + local_x;
    *matrix_y = local_y;
  }

private:
  int chain_length_; // Number of panels per chain
  int parallel_;     // Number of parallel chains
};
