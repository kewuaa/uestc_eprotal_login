#include <iostream>
#include "opencv2/opencv.hpp"

#include "canny.hpp"

using namespace Eigen;


extern "C" double calculate_move_length(
    int fg_width,
    int bg_width,
    int height,
    double* fg_data,
    double* bg_data
);


int main() {
    auto fg = cv::imread("C:/Users/kewuaa/Desktop/1.png", cv::IMREAD_GRAYSCALE);
    auto bg = cv::imread("C:/Users/kewuaa/Desktop/11.png", cv::IMREAD_GRAYSCALE);
    cv::resize(fg, fg, {fg.cols / 4, fg.rows / 4});
    cv::resize(bg, bg, {bg.cols / 4, bg.rows / 4});
    double* fg_data = new double[fg.rows * fg.cols];
    double* bg_data = new double[bg.rows * bg.cols];
    printf("fg: %d rows, %d cols\n", fg.rows, fg.cols);
    printf("bg: %d rows, %d cols\n", bg.rows, bg.cols);
    for (int i = 0; i < fg.rows; i++) {
        for (int j = 0; j < fg.cols; j++) {
            fg_data[i * fg.cols + j] = fg.at<uchar>(i, j);
        }
    }
    for (int i = 0; i < bg.rows; i++) {
        for (int j = 0; j < bg.cols; j++) {
            bg_data[i * bg.cols + j] = bg.at<uchar>(i, j);
        }
    }
    double move_length = calculate_move_length(
        fg.cols,
        bg.cols,
        fg.rows,
        fg_data,
        bg_data
    );
    std::cout << "move length is: " << move_length << "\n";
    delete[] bg_data;
    delete[] fg_data;
}
