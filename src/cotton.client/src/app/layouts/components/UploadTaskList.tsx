import React, { useCallback } from "react";
import { Box } from "@mui/material";
import { Virtuoso } from "react-virtuoso";
import { UploadTaskRow } from "./UploadTaskRow";
import type { UploadTask } from "./uploadQueueUtils";

interface UploadTaskListProps {
  tasks: UploadTask[];
  listHeight: number;
}

const UploadTaskListScroller = React.forwardRef<HTMLDivElement, React.ComponentProps<"div">>(
  (props, ref) => {
    return (
      <Box
        ref={ref}
        {...props}
        sx={{
          overflowX: "hidden",
          scrollbarWidth: "thin",
          "&::-webkit-scrollbar": {
            width: 8,
          },
          "&::-webkit-scrollbar-track": {
            bgcolor: "transparent",
          },
          "&::-webkit-scrollbar-thumb": {
            bgcolor: "action.disabled",
            borderRadius: 1,
          },
          "&::-webkit-scrollbar-thumb:hover": {
            bgcolor: "action.active",
          },
        }}
      />
    );
  },
);

UploadTaskListScroller.displayName = "UploadTaskListScroller";

export const UploadTaskList: React.FC<UploadTaskListProps> = ({
  tasks,
  listHeight,
}) => {
  const renderItem = useCallback((index: number, task: UploadTask) => {
    return (
      <Box px={1.5}>
        <UploadTaskRow task={task} showDivider={index > 0} />
      </Box>
    );
  }, []);

  return (
    <Virtuoso
      style={{ height: listHeight }}
      data={tasks}
      overscan={8}
      defaultItemHeight={88}
      components={{ Scroller: UploadTaskListScroller }}
      computeItemKey={(_, task) => task.id}
      itemContent={renderItem}
    />
  );
};
